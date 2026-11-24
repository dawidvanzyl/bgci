using GameCollection.Application.Commands;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using Jobs.Application.Commands;
using Jobs.Infrastructure.Postgres;
using Jobs.Testing;
using Jobs.Testing.Factories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Shouldly;
using Xunit;

namespace Jobs.Worker.Tests;

public sealed class JobsResetTests : IClassFixture<JobsTestFixture>
{
	private readonly JobsTestContext _context;
	private readonly Mock<JobStorage> _storage = new();
	private readonly Mock<IMonitoringApi> _monitoring = new();
	private readonly Mock<IBackgroundJobClient> _jobs = new();
	private readonly Mock<IBggWriteRequestReset> _requests = new();
	private readonly List<string> _calls = [];
	private readonly List<string> _deleted = [];

	public JobsResetTests(JobsTestFixture fixture)
	{
		_context = fixture.CreateContext();
		_storage.Setup(s => s.GetMonitoringApi()).Returns(_monitoring.Object);
		_monitoring.Setup(m => m.EnqueuedJobs(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>())).Returns(new JobList<EnqueuedJobDto>([]));
		_monitoring.Setup(m => m.ScheduledJobs(It.IsAny<int>(), It.IsAny<int>())).Returns(new JobList<ScheduledJobDto>([]));
		_monitoring.Setup(m => m.ProcessingJobs(It.IsAny<int>(), It.IsAny<int>())).Returns(new JobList<ProcessingJobDto>([]));
		_monitoring.Setup(m => m.FailedJobs(It.IsAny<int>(), It.IsAny<int>())).Returns(new JobList<FailedJobDto>([]));
		_jobs
			.Setup(j => j.ChangeState(It.IsAny<string>(), It.IsAny<IState>(), It.IsAny<string>()))
			.Returns(true)
			.Callback((string id, IState _, string _) =>
			{
				_calls.Add("delete");
				_deleted.Add(id);
			});
		_requests
			.Setup(r => r.ResetAsync(It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask)
			.Callback(() => _calls.Add("truncate"));
	}

	[Fact]
	[Trait("AC", "111UC16")]
	public async Task ResetAsync_ListsLongerThanOnePage_DeletesEveryJobOfEveryList()
	{
		var enqueued = Ids("enq", 1200);
		var scheduled = Ids("sch", 3);
		var processing = Ids("pro", 501);
		var failed = Ids("fai", 2);
		_monitoring
			.Setup(m => m.EnqueuedJobs("bgg", It.IsAny<int>(), It.IsAny<int>()))
			.Returns((string _, int from, int count) => Page(enqueued, from, count, _ => new EnqueuedJobDto()));
		_monitoring
			.Setup(m => m.ScheduledJobs(It.IsAny<int>(), It.IsAny<int>()))
			.Returns((int from, int count) => Page(scheduled, from, count, _ => new ScheduledJobDto()));
		_monitoring
			.Setup(m => m.ProcessingJobs(It.IsAny<int>(), It.IsAny<int>()))
			.Returns((int from, int count) => Page(processing, from, count, _ => new ProcessingJobDto()));
		_monitoring
			.Setup(m => m.FailedJobs(It.IsAny<int>(), It.IsAny<int>()))
			.Returns((int from, int count) => Page(failed, from, count, _ => new FailedJobDto()));

		await Reset(new RunningJobs(_context.Time)).ResetAsync(TestContext.Current.CancellationToken);

		_deleted.Distinct().Order().ShouldBe(enqueued.Concat(scheduled).Concat(processing).Concat(failed).Order());
	}

	[Fact]
	[Trait("AC", "111UC16")]
	public async Task ResetAsync_AJobStays_DeletesThenTruncatesThenWaitsThenDeletesAgainThenTruncatesAgain()
	{
		var enqueued = Ids("enq", 1);
		_monitoring
			.Setup(m => m.EnqueuedJobs("bgg", It.IsAny<int>(), It.IsAny<int>()))
			.Returns((string _, int from, int count) => Page(enqueued, from, count, _ => new EnqueuedJobDto()));
		var runningJobs = new Mock<IRunningJobs>();
		runningJobs
			.Setup(r => r.WaitUntilIdleAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask)
			.Callback(() => _calls.Add("wait"));

		await Reset(runningJobs.Object).ResetAsync(TestContext.Current.CancellationToken);

		_calls.ShouldBe(["delete", "truncate", "wait", "delete", "truncate"]);
		runningJobs.Verify(r => r.WaitUntilIdleAsync(TimeSpan.FromSeconds(35), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	[Trait("AC", "111UC16")]
	public async Task ResetAsync_ABodyEntersAfterTheFirstTruncate_MakesNoBggCallAndWritesNoCompletion()
	{
		var request = BggWriteRequestFactory.CreateAdd();
		var truncated = false;
		_context.Repositories.BggWriteRequestRepositoryMock
			.Setup(r => r.GetAsync(request.Id, It.IsAny<CancellationToken>()))
			.ReturnsAsync(() => truncated ? null : request);
		_context.Repositories.BggWriteRequestRepositoryMock
			.Setup(r => r.StartAttemptAsync(request.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(1);
		_context.Services.BggAddReturns(555);
		var processing = new[] { "late-job" };
		_monitoring
			.Setup(m => m.ProcessingJobs(It.IsAny<int>(), It.IsAny<int>()))
			.Returns((int from, int count) => Page(processing, from, count, _ => new ProcessingJobDto()));
		var handler = _context.ServiceProvider.GetRequiredService<RunBggAddCommandHandler>();
		var truncations = 0;
		_requests
			.Setup(r => r.ResetAsync(It.IsAny<CancellationToken>()))
			.Returns(async () =>
			{
				truncated = true;
				if (++truncations == 1)
				{
					await handler.Handle(new RunBggAddCommand(request.Id, 0), CancellationToken.None);
				}
			});

		await Reset(new RunningJobs(_context.Time)).ResetAsync(TestContext.Current.CancellationToken);

		_context.Services.BggCollectionWriterMock.VerifyNoOtherCalls();
		_context.Repositories.Completions().ShouldBeEmpty();
		_context.Services.MediatorMock.VerifyNoOtherCalls();
	}

	[Fact]
	[Trait("AC", "111UC16")]
	public async Task ResetAsync_ABodyStillRunsAfterTheLimit_ThrowsTimeoutExceptionAndSkipsTheSecondTruncate()
	{
		var time = new FakeTimeProvider();
		var runningJobs = new RunningJobs(time);
		using var running = runningJobs.Enter();

		var reset = Reset(runningJobs).ResetAsync(TestContext.Current.CancellationToken);
		time.Advance(TimeSpan.FromSeconds(36));

		await Should.ThrowAsync<TimeoutException>(() => reset);
		_requests.Verify(r => r.ResetAsync(It.IsAny<CancellationToken>()), Times.Once);
	}

	private JobsReset Reset(IRunningJobs runningJobs) =>
		new(_storage.Object, _jobs.Object, _requests.Object, runningJobs);

	private static string[] Ids(string prefix, int count) =>
		Enumerable.Range(0, count).Select(i => $"{prefix}-{i}").ToArray();

	private static JobList<T> Page<T>(string[] ids, int from, int count, Func<string, T> create)
		where T : class =>
		new(ids.Skip(from).Take(count).Select(id => new KeyValuePair<string, T>(id, create(id))));
}