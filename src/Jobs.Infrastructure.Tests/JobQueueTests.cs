using Hangfire;
using Jobs.Infrastructure.Jobs;
using Shouldly;
using System.Reflection;
using Xunit;

namespace Jobs.Infrastructure.Tests;

public sealed class JobQueueTests
{
	[Theory]
	[InlineData(typeof(IBggAddJob))]
	[InlineData(typeof(IBggRemoveJob))]
	[Trait("AC", "111UC8")]
	public void RunAsync_EveryJob_KeepsItsRetriesOnTheBggQueue(Type job)
	{
		var queue = job.GetMethod("RunAsync")!.GetCustomAttribute<QueueAttribute>();

		queue.ShouldNotBeNull();
		queue.Queue.ShouldBe("bgg");
	}
}
