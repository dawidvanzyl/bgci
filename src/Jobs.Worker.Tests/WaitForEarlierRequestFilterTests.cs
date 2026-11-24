using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Jobs.Infrastructure.Jobs;
using Jobs.Worker.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace Jobs.Worker.Tests;

public sealed class WaitForEarlierRequestFilterTests
{
	private readonly Mock<IStorageConnection> _connection = new();

	[Fact]
	[Trait("AC", "112UC6")]
	public void OnStateElection_FailedWithTheWaitException_BecomesScheduledAfterTheDelay()
	{
		var filter = new WaitForEarlierRequestFilter(TimeSpan.FromSeconds(7));
		var context = Elect(new FailedState(new WaitForEarlierRequestException(Guid.NewGuid())));

		filter.OnStateElection(context);

		var scheduled = context.CandidateState.ShouldBeOfType<ScheduledState>();
		scheduled.EnqueueAt.ShouldBeGreaterThan(DateTime.UtcNow.AddSeconds(5));
		scheduled.EnqueueAt.ShouldBeLessThan(DateTime.UtcNow.AddSeconds(9));
	}

	[Fact]
	[Trait("AC", "112UC6")]
	public void OnStateElection_FailedWithAnotherException_StaysFailed()
	{
		var filter = new WaitForEarlierRequestFilter(TimeSpan.FromSeconds(7));
		var failed = new FailedState(new InvalidOperationException("BGG down"));
		var context = Elect(failed);

		filter.OnStateElection(context);

		context.CandidateState.ShouldBeSameAs(failed);
	}

	[Fact]
	[Trait("AC", "112UC6")]
	public void OnStateElection_AnotherStateEntirely_IsLeftUnchanged()
	{
		var filter = new WaitForEarlierRequestFilter(TimeSpan.FromSeconds(7));
		var succeeded = new SucceededState(null, 0, 0);
		var context = Elect(succeeded);

		filter.OnStateElection(context);

		context.CandidateState.ShouldBeSameAs(succeeded);
	}

	[Fact]
	[Trait("AC", "112UC6")]
	public void GlobalFilters_AFailedWaitReachesTheRetryPolicyAsScheduledAndUsesNoRetry()
	{
		new ServiceCollection().AddJobsWorker(new ConfigurationBuilder().Build());
		var job = Job.FromExpression<IBggAddJob>(j => j.RunAsync(Guid.NewGuid(), null, CancellationToken.None));
		var electFilters = ElectFilters(job);
		var context = Elect(new FailedState(new WaitForEarlierRequestException(Guid.NewGuid())));

		foreach (var filter in electFilters)
		{
			filter.OnStateElection(context);
		}

		context.CandidateState.ShouldBeOfType<ScheduledState>();
		_connection.Verify(c => c.SetJobParameter(It.IsAny<string>(), "RetryCount", It.IsAny<string>()), Times.Never);
	}

	[Fact]
	[Trait("AC", "112UC6")]
	public void AddJobsWorker_RegistersOneWaitFilterThatRunsBeforeTheRetryPolicy()
	{
		new ServiceCollection().AddJobsWorker(new ConfigurationBuilder().Build());
		new ServiceCollection().AddJobsWorker(new ConfigurationBuilder().Build());
		var job = Job.FromExpression<IBggAddJob>(j => j.RunAsync(Guid.NewGuid(), null, CancellationToken.None));

		var electFilters = ElectFilters(job);

		electFilters.OfType<WaitForEarlierRequestFilter>().ShouldHaveSingleItem();
		var waitIndex = electFilters.FindIndex(f => f is WaitForEarlierRequestFilter);
		var retryIndex = electFilters.FindIndex(f => f is AutomaticRetryAttribute);
		waitIndex.ShouldBeLessThan(retryIndex);
	}

	private static List<IElectStateFilter> ElectFilters(Job job) =>
		JobFilterProviders.Providers.GetFilters(job)
			.OrderBy(f => f.Order)
			.ThenBy(f => f.Scope)
			.Select(f => f.Instance)
			.OfType<IElectStateFilter>()
			.ToList();

	private ElectStateContext Elect(IState candidate)
	{
		var backgroundJob = new BackgroundJob("1", Job.FromExpression<IBggAddJob>(j => j.RunAsync(Guid.NewGuid(), null, CancellationToken.None)), DateTime.UtcNow);
		var apply = new ApplyStateContext(
			new Mock<JobStorage>().Object,
			_connection.Object,
			new Mock<IWriteOnlyTransaction>().Object,
			backgroundJob,
			candidate,
			ProcessingState.StateName);
		return new ElectStateContext(apply);
	}
}