using GameCollection.Application.Abstractions;
using Hangfire;
using Hangfire.States;
using Jobs.Application.Models;
using Jobs.Infrastructure.Jobs;

namespace Jobs.Infrastructure.Postgres;

public sealed class HangfireBggWriteQueue : IBggWriteQueue
{
	private readonly PostgresBggWriteRequestRepository _requests;
	private readonly IBackgroundJobClient _jobs;
	private readonly TimeProvider _timeProvider;

	public HangfireBggWriteQueue(
		PostgresBggWriteRequestRepository requests,
		IBackgroundJobClient jobs,
		TimeProvider timeProvider)
	{
		_requests = requests;
		_jobs = jobs;
		_timeProvider = timeProvider;
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

	public Task QueueRemoveAsync(Guid gameId, int bggId, long? bggCollId, CancellationToken cancellationToken = default) =>
		QueueAsync(
			BggWriteKind.Remove,
			bggId,
			gameId,
			string.Empty,
			bggCollId,
			requestId => _jobs.Create<IBggRemoveJob>(j => j.RunAsync(requestId, null, CancellationToken.None), new EnqueuedState(BggQueue.Name)),
			cancellationToken);

	private async Task QueueAsync(
		BggWriteKind kind,
		int bggId,
		Guid gameId,
		string name,
		long? collectionItemId,
		Func<Guid, string> createJob,
		CancellationToken cancellationToken)
	{
		var requestId = Guid.NewGuid();
		var key = $"{kind.ToString().ToLowerInvariant()}:{gameId}";
		var created = await _requests.CreateAsync(
			requestId,
			kind,
			bggId,
			gameId,
			name,
			collectionItemId,
			BggAccounts.Owner,
			key,
			_timeProvider.GetUtcNow(),
			cancellationToken);
		if (!created)
			return;

		string jobId;
		try
		{
			jobId = createJob(requestId);
		}
		catch
		{
			await _requests.DeleteAsync(requestId, cancellationToken);
			throw;
		}

		await _requests.SetJobAsync(requestId, jobId, cancellationToken);
	}
}
