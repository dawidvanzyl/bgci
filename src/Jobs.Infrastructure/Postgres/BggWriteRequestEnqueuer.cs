using Hangfire;
using Jobs.Application.Models;

namespace Jobs.Infrastructure.Postgres;

internal sealed class BggWriteRequestEnqueuer
{
	private readonly PostgresBggWriteRequestRepository _requests;
	private readonly IBackgroundJobClient _jobs;
	private readonly TimeProvider _timeProvider;

	public BggWriteRequestEnqueuer(
		PostgresBggWriteRequestRepository requests,
		IBackgroundJobClient jobs,
		TimeProvider timeProvider)
	{
		_requests = requests;
		_jobs = jobs;
		_timeProvider = timeProvider;
	}

	public async Task<bool> EnqueueAsync(
		Guid requestId,
		BggWriteKind kind,
		int bggId,
		Guid? gameId,
		string name,
		long? collectionItemId,
		string account,
		string idempotencyKey,
		Func<Guid, string> createJob,
		CancellationToken cancellationToken)
	{
		var created = await _requests.CreateAsync(
			requestId,
			kind,
			bggId,
			gameId,
			name,
			collectionItemId,
			account,
			idempotencyKey,
			_timeProvider.GetUtcNow(),
			cancellationToken);
		if (!created)
			return false;

		string jobId;
		try
		{
			jobId = createJob(requestId);
		}
		catch
		{
			await _requests.DeleteAsync(requestId, CancellationToken.None);
			throw;
		}

		try
		{
			await _requests.SetJobAsync(requestId, jobId, cancellationToken);
		}
		catch
		{
			_jobs.Delete(jobId);
			await _requests.DeleteAsync(requestId, CancellationToken.None);
			throw;
		}

		return true;
	}
}