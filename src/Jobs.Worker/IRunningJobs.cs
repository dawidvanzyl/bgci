namespace Jobs.Worker;

public interface IRunningJobs
{
	IDisposable Enter();

	Task WaitUntilIdleAsync(TimeSpan limit, CancellationToken cancellationToken = default);
}