using Jobs.Application.Abstractions;
using Jobs.Application.LiveTest;
using Jobs.Application.Models;
using MediatR;
using System.Text.Json;

namespace Jobs.Application.Commands;

public class RunBggLiveTestCommandHandler : IRequestHandler<RunBggLiveTestCommand>
{
	public const string JobType = "BGG live test";

	private const string _testAccount = "test";
	private const int _bggId = 3179;
	private const int _listings = 6;
	private const int _confirmingListings = 3;

	private static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan _runLimit = TimeSpan.FromMinutes(20);
	private static readonly TimeSpan _cleanupLimit = TimeSpan.FromMinutes(8);

	private readonly IBggTestWriteQueue _queue;
	private readonly IBggWriteRequestRepository _requests;
	private readonly IBggTestCollectionReader _reader;
	private readonly IBggTestAccount _account;
	private readonly ILiveWritableGames _games;
	private readonly IBggLiveTestLog _log;
	private readonly TimeProvider _timeProvider;

	public RunBggLiveTestCommandHandler(
		IBggTestWriteQueue queue,
		IBggWriteRequestRepository requests,
		IBggTestCollectionReader reader,
		IBggTestAccount account,
		ILiveWritableGames games,
		IBggLiveTestLog log,
		TimeProvider timeProvider)
	{
		_queue = queue;
		_requests = requests;
		_reader = reader;
		_account = account;
		_games = games;
		_log = log;
		_timeProvider = timeProvider;
	}

	public async Task Handle(RunBggLiveTestCommand command, CancellationToken cancellationToken)
	{
		var startedAt = _timeProvider.GetUtcNow();
		var run = new BggLiveTestRun();

		var game = CheckGuards(run);
		if (game is not null)
		{
			await RunAsync(run, game, cancellationToken);
			await CleanUpAfterAsync(run, game, cancellationToken);
		}

		await FinishAsync(run, startedAt, cancellationToken);
	}

	private LiveTestGame? CheckGuards(BggLiveTestRun run)
	{
		var refusal = _account.RefusalReason();
		if (refusal is not null)
		{
			run.Refusal = $"The live BGG write test refuses to run: {refusal}";
			return null;
		}

		var game = _games.Find(_bggId);
		if (game is null || !game.LiveWritable)
		{
			run.Refusal = $"The live BGG write test refuses to run: the game data set has no liveWritable entry with id {_bggId}.";
			return null;
		}

		return game;
	}

	private async Task RunAsync(BggLiveTestRun run, LiveTestGame game, CancellationToken cancellationToken)
	{
		using var limit = new CancellationTokenSource(_runLimit, _timeProvider);
		using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, limit.Token);
		try
		{
			await CleanUpBeforeAsync(run, game, linked.Token);
			await AddAndRemoveAsync(run, game, linked.Token);
		}
		catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
		{
			run.Failures.Add(Classify(ex, run.Phase));
		}
	}

	private async Task CleanUpBeforeAsync(BggLiveTestRun run, LiveTestGame game, CancellationToken cancellationToken)
	{
		run.Phase = BggLiveTestSteps.Cleanup;
		await RemoveAllAsync(run, await _reader.ListCollectionItemIdsAsync(_bggId, cancellationToken), cancellationToken);
		if (!await WaitUntilAsync(listed => listed.Count == 0, cancellationToken))
			throw Fail(BggLiveTestSteps.Cleanup, BggLiveTestFailureKind.StillListed, $"{game.Name} is still listed after the cleanup before the add.");
	}

	private async Task AddAndRemoveAsync(BggLiveTestRun run, LiveTestGame game, CancellationToken cancellationToken)
	{
		run.Phase = BggLiveTestSteps.Add;
		run.AddSent = true;
		var addRequestId = await QueueAsync(run, _queue.QueueAddAsync(_bggId, game.Name, cancellationToken));
		run.AddRequestId = addRequestId;
		var added = await WaitForSentAsync(run, addRequestId, cancellationToken);
		var collectionItemId = added.CollectionItemId
			?? throw Fail(BggLiveTestSteps.Add, BggLiveTestFailureKind.WriteRefused, "The add request finished without a collection item id.");

		if (!await WaitUntilAsync(listed => listed.Contains(collectionItemId), cancellationToken))
			throw Fail(BggLiveTestSteps.Lookup, BggLiveTestFailureKind.LookupFailed, $"{game.Name} was not listed after the add.");

		run.Phase = BggLiveTestSteps.Remove;
		await RemoveAsync(run, collectionItemId, cancellationToken);
		if (!await WaitUntilAsync(listed => listed.Count == 0, cancellationToken))
			throw Fail(BggLiveTestSteps.Remove, BggLiveTestFailureKind.StillListed, $"{game.Name} is still listed after the remove.");

		run.ConfirmedGone = true;
	}

	private async Task CleanUpAfterAsync(BggLiveTestRun run, LiveTestGame game, CancellationToken cancellationToken)
	{
		if (run.LoginFailedBeforeAdd)
			return;

		using var limit = new CancellationTokenSource(_cleanupLimit, _timeProvider);
		using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, limit.Token);
		run.Phase = BggLiveTestSteps.Cleanup;
		try
		{
			if (run.AddRequestId is { } addRequestId)
				await _queue.CancelAsync(addRequestId, linked.Token);

			var failure = run.AddSent && !run.ConfirmedGone
				? await SweepForLateListingAsync(run, game, linked.Token)
				: await RemoveListedOnceAsync(run, linked.Token);
			if (failure is not null)
				run.Failures.Add(failure);
		}
		catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
		{
			run.Failures.Add(Classify(ex, run.Phase));
		}
	}

	// BGG can list an added item late, so an empty listing does not end the sweep.
	private async Task<BggLiveTestFailure?> SweepForLateListingAsync(BggLiveTestRun run, LiveTestGame game, CancellationToken cancellationToken)
	{
		IReadOnlyList<long> last = [];
		for (var listing = 1; listing <= _listings; listing++)
		{
			if (listing > 1)
				await Task.Delay(_pollInterval, _timeProvider, cancellationToken);

			last = await _reader.ListCollectionItemIdsAsync(_bggId, cancellationToken);
			await RemoveAllAsync(run, last, cancellationToken);
		}

		if (last.Count == 0)
			return null;

		for (var confirming = 1; confirming <= _confirmingListings; confirming++)
		{
			await Task.Delay(_pollInterval, _timeProvider, cancellationToken);
			var listed = await _reader.ListCollectionItemIdsAsync(_bggId, cancellationToken);
			if (listed.Count == 0)
				return null;

			await RemoveAllAsync(run, listed, cancellationToken);
		}

		return new BggLiveTestFailure(BggLiveTestSteps.Cleanup, BggLiveTestFailureKind.StillListed, $"{game.Name} was still listed after the cleanup.");
	}

	private async Task<BggLiveTestFailure?> RemoveListedOnceAsync(BggLiveTestRun run, CancellationToken cancellationToken)
	{
		await RemoveAllAsync(run, await _reader.ListCollectionItemIdsAsync(_bggId, cancellationToken), cancellationToken);
		return null;
	}

	private async Task RemoveAllAsync(BggLiveTestRun run, IReadOnlyList<long> collectionItemIds, CancellationToken cancellationToken)
	{
		foreach (var collectionItemId in collectionItemIds.Where(id => !run.Removed.Contains(id)).ToList())
			await RemoveAsync(run, collectionItemId, cancellationToken);
	}

	private async Task RemoveAsync(BggLiveTestRun run, long collectionItemId, CancellationToken cancellationToken)
	{
		run.Removed.Add(collectionItemId);
		var requestId = await QueueAsync(run, _queue.QueueRemoveAsync(_bggId, collectionItemId, cancellationToken));
		await WaitForSentAsync(run, requestId, cancellationToken);
	}

	private static async Task<Guid> QueueAsync(BggLiveTestRun run, Task<Guid> queued)
	{
		var requestId = await queued;
		run.RequestIds.Add(requestId);
		return requestId;
	}

	private async Task<BggWriteRequest> WaitForSentAsync(BggLiveTestRun run, Guid requestId, CancellationToken cancellationToken)
	{
		while (true)
		{
			var request = await _requests.GetAsync(requestId, cancellationToken)
				?? throw new BggLiveTestStepFailedException(BggLiveTestFailureClassifier.ClassifyMissingRequest(requestId, run.Phase));

			switch (request.Status)
			{
				case BggWriteRequestStatus.Sent:
					return request;
				case BggWriteRequestStatus.Failed:
					var error = await _requests.GetErrorAsync(requestId, cancellationToken) ?? "The job failed without a recorded error.";
					throw new BggLiveTestStepFailedException(BggLiveTestFailureClassifier.ClassifyJobError(error, run.Phase));
				case BggWriteRequestStatus.Cancelled:
					throw new BggLiveTestStepFailedException(BggLiveTestFailureClassifier.ClassifyJobError("The request was cancelled.", run.Phase));
			}

			await Task.Delay(_pollInterval, _timeProvider, cancellationToken);
		}
	}

	private async Task<bool> WaitUntilAsync(Func<IReadOnlyList<long>, bool> reached, CancellationToken cancellationToken)
	{
		for (var listing = 1; listing <= _listings; listing++)
		{
			if (listing > 1)
				await Task.Delay(_pollInterval, _timeProvider, cancellationToken);

			if (reached(await _reader.ListCollectionItemIdsAsync(_bggId, cancellationToken)))
				return true;
		}

		return false;
	}

	private async Task FinishAsync(BggLiveTestRun run, DateTimeOffset startedAt, CancellationToken cancellationToken)
	{
		var message = run.Message is { } failure ? _account.Redact(failure) : null;
		var payload = JsonSerializer.Serialize(new { runId = run.Id, bggId = _bggId, requestIds = run.RequestIds });
		await _log.RecordAsync(
			new BggLiveTestRecord(JobType, _testAccount, payload, message is null ? "passed" : null, message, startedAt, _timeProvider.GetUtcNow()),
			cancellationToken);

		if (message is not null)
			throw new BggLiveTestFailedException(message);
	}

	private static BggLiveTestStepFailedException Fail(string step, BggLiveTestFailureKind kind, string detail) =>
		new(new BggLiveTestFailure(step, kind, detail));

	private static BggLiveTestFailure Classify(Exception exception, string phase) => exception switch
	{
		BggLiveTestStepFailedException step => step.Failure,
		BggTestReadException read => BggLiveTestFailureClassifier.ClassifyRead(read, phase),
		OperationCanceledException => BggLiveTestFailureClassifier.ClassifyCancelled(phase),
		_ => BggLiveTestFailureClassifier.ClassifyUnexpected(exception, phase)
	};
}