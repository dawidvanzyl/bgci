using Jobs.Application.Abstractions;
using Jobs.Application.Models;
using MediatR;

namespace Jobs.Application.Commands;

public class RunBggRemoveCommandHandler : IRequestHandler<RunBggRemoveCommand, BggJobOutcome>
{
	public const string JobType = "BGG remove";

	private readonly IBggWriteRequestRepository _requests;
	private readonly IBggCollectionWriter _writer;
	private readonly TimeProvider _timeProvider;

	public RunBggRemoveCommandHandler(
		IBggWriteRequestRepository requests,
		IBggCollectionWriter writer,
		TimeProvider timeProvider)
	{
		_requests = requests;
		_writer = writer;
		_timeProvider = timeProvider;
	}

	public async Task<BggJobOutcome> Handle(RunBggRemoveCommand command, CancellationToken cancellationToken)
	{
		var request = await _requests.GetAsync(command.RequestId, cancellationToken);
		if (request is null || request.Status is BggWriteRequestStatus.Sent or BggWriteRequestStatus.Cancelled)
			return BggJobOutcome.Finished;

		if (await _requests.HasUnfinishedEarlierRequestAsync(request.Id, cancellationToken))
			return BggJobOutcome.WaitingForEarlierRequest;

		var startedAt = _timeProvider.GetUtcNow();
		var attempts = request.Attempts;
		try
		{
			var started = await _requests.StartAttemptAsync(request.Id, startedAt, cancellationToken);
			if (started is null)
				return BggJobOutcome.Finished;

			attempts = started.Value;
			await RemoveAsync(request, attempts, startedAt, cancellationToken);
			return BggJobOutcome.Finished;
		}
		catch (Exception ex) when (BggJobPolicy.IsFinalAttempt(command.RetryCount) && !IsCancelled(ex, cancellationToken))
		{
			await _requests.CompleteAsync(
				BggWriteCompletions.Failed(request, JobType, ex.Message, attempts, startedAt, _timeProvider.GetUtcNow()),
				cancellationToken);
			throw;
		}
	}

	private async Task RemoveAsync(BggWriteRequest request, int attempts, DateTimeOffset startedAt, CancellationToken cancellationToken)
	{
		var listedItemId = await _writer.FindCollectionItemIdAsync(request.BggId, cancellationToken);
		if (listedItemId is null)
		{
			await _requests.CompleteAsync(
				BggWriteCompletions.Sent(request, JobType, request.CollectionItemId, "already gone from the BGG collection", attempts, startedAt, _timeProvider.GetUtcNow()),
				cancellationToken);
			return;
		}

		var collectionItemId = request.CollectionItemId ?? listedItemId.Value;
		await _writer.RemoveAsync(collectionItemId, cancellationToken);
		await _requests.CompleteAsync(
			BggWriteCompletions.Sent(request, JobType, collectionItemId, $"removed collection item {collectionItemId}", attempts, startedAt, _timeProvider.GetUtcNow()),
			cancellationToken);
	}

	private static bool IsCancelled(Exception exception, CancellationToken cancellationToken) =>
		exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
}