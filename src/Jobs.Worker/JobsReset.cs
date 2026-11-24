using Hangfire;
using Jobs.Infrastructure;
using Jobs.Infrastructure.Postgres;

namespace Jobs.Worker;

public sealed class JobsReset : IJobsReset
{
	public static readonly TimeSpan RunningJobLimit = TimeSpan.FromSeconds(35);

	private const int _pageSize = 500;

	private readonly JobStorage _jobStorage;
	private readonly IBackgroundJobClient _jobs;
	private readonly IBggWriteRequestReset _requests;
	private readonly IRunningJobs _runningJobs;

	public JobsReset(
		JobStorage jobStorage,
		IBackgroundJobClient jobs,
		IBggWriteRequestReset requests,
		IRunningJobs runningJobs)
	{
		_jobStorage = jobStorage;
		_jobs = jobs;
		_requests = requests;
		_runningJobs = runningJobs;
	}

	public async Task ResetAsync(CancellationToken cancellationToken = default)
	{
		DeleteEveryJob();
		await _requests.ResetAsync(cancellationToken);
		await _runningJobs.WaitUntilIdleAsync(RunningJobLimit, cancellationToken);
		DeleteEveryJob();
		await _requests.ResetAsync(cancellationToken);
	}

	private void DeleteEveryJob()
	{
		var monitoring = _jobStorage.GetMonitoringApi();
		DeleteAll(from => monitoring.EnqueuedJobs(BggQueue.Name, from, _pageSize).Select(job => job.Key).ToList());
		DeleteAll(from => monitoring.ScheduledJobs(from, _pageSize).Select(job => job.Key).ToList());
		DeleteAll(from => monitoring.ProcessingJobs(from, _pageSize).Select(job => job.Key).ToList());
		DeleteAll(from => monitoring.FailedJobs(from, _pageSize).Select(job => job.Key).ToList());
	}

	private void DeleteAll(Func<int, List<string>> readPage)
	{
		var jobIds = new List<string>();
		for (var from = 0; ; from += _pageSize)
		{
			var page = readPage(from);
			if (page.Count == 0)
			{
				break;
			}

			jobIds.AddRange(page);
		}

		foreach (var jobId in jobIds)
		{
			_jobs.Delete(jobId);
		}
	}
}
