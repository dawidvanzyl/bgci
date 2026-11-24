using Hangfire;
using Jobs.Application;
using Microsoft.Extensions.Configuration;

namespace Jobs.Worker;

internal static class BggRetryPolicy
{
	public static AutomaticRetryAttribute Create(IConfiguration configuration)
	{
		var attribute = new AutomaticRetryAttribute
		{
			Attempts = BggJobPolicy.MaxAttempts - 1,
			OnAttemptsExceeded = AttemptsExceededAction.Fail
		};

		var delays = configuration.GetSection("Jobs:RetryDelaysSeconds").Get<int[]>();
		if (delays is { Length: > 0 })
		{
			attribute.DelaysInSeconds = delays;
		}

		return attribute;
	}
}