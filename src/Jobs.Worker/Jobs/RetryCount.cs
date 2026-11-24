using Hangfire.Server;

namespace Jobs.Worker.Jobs;

internal static class RetryCount
{
	public static int Of(PerformContext? context) => context?.GetJobParameter<int>("RetryCount") ?? 0;
}
