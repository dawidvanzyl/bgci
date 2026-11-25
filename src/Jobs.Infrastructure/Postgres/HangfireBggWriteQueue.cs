using GameCollection.Application.Abstractions;
using Hangfire;
using Hangfire.States;
using Jobs.Application.Commands;
using Jobs.Application.Models;
using Jobs.Infrastructure.Jobs;

namespace Jobs.Infrastructure.Postgres;

public sealed class HangfireBggWriteQueue : IBggWriteQueue
{
	private readonly PostgresBggWriteRequestRepository _requests;
	private readonly IBackgroundJobClient _jobs;
	private readonly TimeProvider _timeProvider;
	private readonly BggWriteRequestEnqueuer _enqueuer;

	public HangfireBggWriteQueue(
		PostgresBggWriteRequestRepository requests,
		IBackgroundJobClient jobs,
		TimeProvider timeProvider)
	{
		_requests = requests;
		_jobs = jobs;
		_timeProvider = timeProvider;
		_enqueuer = new BggWriteRequestEnqueuer(requests, jobs, timeProvider);
	}

	public Task QueueAddAsync(Guid gameId, int bggId, string name, CancellationToken cancellationToken = default) =>
		QueueAsync(
			BggWriteKind.Add,
			bggId,
			gameId,
			name,
			null,
			requestId => _jobs.Create<IBggAddJob>(j => j.RunAsync(requestId, null, CancellationToken.None), new EnqueuedState(BggQueue.Name)),
			cancellationToken);

	public async Task QueueRemoveAsync(Guid gameId, int bggId, long? bggCollId, CancellationToken cancellationToken = default)
	{
		var cancelledAttempts = await _requests.CancelUnfinishedAddAsync(
			gameId,
			BggAccounts.Owner,
			RunBggAddCommandHandler.JobType,
			_timeProvider.GetUtcNow(),
			cancellationToken);
		if (cancelledAttempts == 0)
			return;

		await QueueAsync(
			BggWriteKind.Remove,
			bggId,
			gameId,
			string.Empty,
			bggCollId,
			requestId => _jobs.Create<IBggRemoveJob>(j => j.RunAsync(requestId, null, CancellationToken.None), new EnqueuedState(BggQueue.Name)),
			cancellationToken);
	}

	public Task<bool> HasUnresolvedRemoveAsync(int bggId, CancellationToken cancellationToken = default) =>
		_requests.HasUnresolvedRemoveAsync(BggAccounts.Owner, bggId, cancellationToken);

	public Task<IReadOnlyList<int>> GetPendingRemoveBggIdsAsync(CancellationToken cancellationToken = default) =>
		_requests.GetPendingRemoveBggIdsAsync(BggAccounts.Owner, cancellationToken);

	private async Task QueueAsync(
		BggWriteKind kind,
		int bggId,
		Guid gameId,
		string name,
		long? collectionItemId,
		Func<Guid, string> createJob,
		CancellationToken cancellationToken)
	{
		await _enqueuer.EnqueueAsync(
			Guid.NewGuid(),
			kind,
			bggId,
			gameId,
			name,
			collectionItemId,
			BggAccounts.Owner,
			$"{kind.ToString().ToLowerInvariant()}:{gameId}",
			createJob,
			cancellationToken);
	}
}