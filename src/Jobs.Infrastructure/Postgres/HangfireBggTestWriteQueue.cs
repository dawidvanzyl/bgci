using Hangfire;
using Hangfire.States;
using Jobs.Application.Abstractions;
using Jobs.Application.Commands;
using Jobs.Application.Models;
using Jobs.Infrastructure.Jobs;

namespace Jobs.Infrastructure.Postgres;

public sealed class HangfireBggTestWriteQueue : IBggTestWriteQueue
{
	private readonly PostgresBggWriteRequestRepository _requests;
	private readonly IBackgroundJobClient _jobs;
	private readonly TimeProvider _timeProvider;
	private readonly BggWriteRequestEnqueuer _enqueuer;

	public HangfireBggTestWriteQueue(
		PostgresBggWriteRequestRepository requests,
		IBackgroundJobClient jobs,
		TimeProvider timeProvider)
	{
		_requests = requests;
		_jobs = jobs;
		_timeProvider = timeProvider;
		_enqueuer = new BggWriteRequestEnqueuer(requests, jobs, timeProvider);
	}

	public Task<Guid> QueueAddAsync(int bggId, string name, CancellationToken cancellationToken = default) =>
		QueueAsync(
			BggWriteKind.Add,
			bggId,
			name,
			null,
			requestId => _jobs.Create<IBggAddJob>(j => j.RunAsync(requestId, null, CancellationToken.None), new EnqueuedState(BggQueue.Name)),
			cancellationToken);

	public Task<Guid> QueueRemoveAsync(int bggId, long collectionItemId, CancellationToken cancellationToken = default) =>
		QueueAsync(
			BggWriteKind.Remove,
			bggId,
			string.Empty,
			collectionItemId,
			requestId => _jobs.Create<IBggRemoveJob>(j => j.RunAsync(requestId, null, CancellationToken.None), new EnqueuedState(BggQueue.Name)),
			cancellationToken);

	public Task<bool> CancelAsync(Guid requestId, CancellationToken cancellationToken = default) =>
		_requests.CancelAsync(requestId, RunBggAddCommandHandler.JobType, _timeProvider.GetUtcNow(), cancellationToken);

	private async Task<Guid> QueueAsync(
		BggWriteKind kind,
		int bggId,
		string name,
		long? collectionItemId,
		Func<Guid, string> createJob,
		CancellationToken cancellationToken)
	{
		var requestId = Guid.NewGuid();
		var key = $"test:{kind.ToString().ToLowerInvariant()}:{requestId}";
		var created = await _enqueuer.EnqueueAsync(
			requestId,
			kind,
			bggId,
			null,
			name,
			collectionItemId,
			BggAccounts.Test,
			key,
			createJob,
			cancellationToken);

		return created
			? requestId
			: throw new InvalidOperationException($"The live test request {requestId} was not created.");
	}
}