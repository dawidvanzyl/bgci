using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Jobs.Worker.Tests;

public sealed class RetryPolicyTests
{
	[Fact]
	[Trait("AC", "111UC8")]
	public void Create_NoSettings_AllowsTwoRetriesThenFailsTheJob()
	{
		var policy = BggRetryPolicy.Create(Configuration([]));

		policy.Attempts.ShouldBe(2);
		policy.OnAttemptsExceeded.ShouldBe(AttemptsExceededAction.Fail);
		policy.DelaysInSeconds.ShouldBeNull();
	}

	[Fact]
	[Trait("AC", "111UC8")]
	public void Create_DelaysConfigured_UsesThem()
	{
		var policy = BggRetryPolicy.Create(Configuration(new()
		{
			["Jobs:RetryDelaysSeconds:0"] = "2",
			["Jobs:RetryDelaysSeconds:1"] = "5"
		}));

		policy.DelaysInSeconds.ShouldBe([2, 5]);
		policy.Attempts.ShouldBe(2);
	}

	[Fact]
	[Trait("AC", "111UC8")]
	public void AddJobsWorker_ReplacesTheGlobalRetryPolicyWithOneInstance()
	{
		new ServiceCollection().AddJobsWorker(Configuration([]));

		var policies = GlobalJobFilters.Filters.Select(f => f.Instance).OfType<AutomaticRetryAttribute>().ToList();

		var policy = policies.ShouldHaveSingleItem();
		policy.Attempts.ShouldBe(2);
		policy.OnAttemptsExceeded.ShouldBe(AttemptsExceededAction.Fail);
	}

	private static IConfiguration Configuration(Dictionary<string, string?> values) =>
		new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}