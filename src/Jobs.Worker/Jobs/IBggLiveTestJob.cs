using Hangfire;
using Hangfire.Server;

namespace Jobs.Worker.Jobs;

public interface IBggLiveTestJob
{
	[JobDisplayName("BGG live write test")]
	[Queue(BggLiveTestQueue.Name)]
	[AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
	[DisableConcurrentExecution(timeoutInSeconds: 60)]
	Task RunAsync(PerformContext? context, CancellationToken cancellationToken);
}