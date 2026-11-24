using Hangfire;
using Hangfire.Server;

namespace Jobs.Infrastructure.Jobs;

public interface IBggAddJob
{
	[JobDisplayName("BGG add")]
	[Queue(BggQueue.Name)]
	Task RunAsync(Guid requestId, PerformContext? context, CancellationToken cancellationToken);
}