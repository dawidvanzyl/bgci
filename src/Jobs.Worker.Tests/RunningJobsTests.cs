using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace Jobs.Worker.Tests;

public sealed class RunningJobsTests
{
	private readonly FakeTimeProvider _time = new();
	private readonly RunningJobs _runningJobs;

	public RunningJobsTests()
	{
		_runningJobs = new RunningJobs(_time);
	}

	[Fact]
	[Trait("AC", "111UC16")]
	public async Task WaitUntilIdleAsync_NoJobRuns_CompletesAtOnce()
	{
		await _runningJobs.WaitUntilIdleAsync(TimeSpan.FromSeconds(35), TestContext.Current.CancellationToken);
	}

	[Fact]
	[Trait("AC", "111UC16")]
	public async Task WaitUntilIdleAsync_TwoJobsRun_CompletesWhenTheLastOneLeaves()
	{
		var first = _runningJobs.Enter();
		var second = _runningJobs.Enter();

		var wait = _runningJobs.WaitUntilIdleAsync(TimeSpan.FromSeconds(35), TestContext.Current.CancellationToken);
		first.Dispose();
		wait.IsCompleted.ShouldBeFalse();
		second.Dispose();

		await wait;
	}

	[Fact]
	[Trait("AC", "111UC16")]
	public void WaitUntilIdleAsync_ALeaveIsReportedTwice_CountsItOnce()
	{
		var first = _runningJobs.Enter();
		using var second = _runningJobs.Enter();

		var wait = _runningJobs.WaitUntilIdleAsync(TimeSpan.FromSeconds(35), TestContext.Current.CancellationToken);
		first.Dispose();
		first.Dispose();

		wait.IsCompleted.ShouldBeFalse();
	}

	[Fact]
	[Trait("AC", "111UC16")]
	public async Task WaitUntilIdleAsync_AJobStaysPastTheLimit_ThrowsTimeoutException()
	{
		using var running = _runningJobs.Enter();

		var wait = _runningJobs.WaitUntilIdleAsync(TimeSpan.FromSeconds(35), TestContext.Current.CancellationToken);
		_time.Advance(TimeSpan.FromSeconds(36));

		await Should.ThrowAsync<TimeoutException>(() => wait);
	}
}