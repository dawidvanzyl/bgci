namespace Jobs.Worker;

public interface IJobsReset
{
	Task ResetAsync(CancellationToken cancellationToken = default);
}