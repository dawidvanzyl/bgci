using Hangfire;
using Hangfire.Server;

namespace Jobs.Infrastructure.Jobs;

public interface IBggAddJob
{
	[JobDisplayName("BGG add")]
	Task RunAsync(Guid requestId, PerformContext? context, CancellationToken cancellationToken);
}