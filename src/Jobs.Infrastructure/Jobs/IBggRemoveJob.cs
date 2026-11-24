using Hangfire;
using Hangfire.Server;

namespace Jobs.Infrastructure.Jobs;

public interface IBggRemoveJob
{
	[JobDisplayName("BGG remove")]
	Task RunAsync(Guid requestId, PerformContext? context, CancellationToken cancellationToken);
}
