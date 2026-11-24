using GameCollection.Testing.Postgres;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Jobs.Infrastructure.Jobs;
using Jobs.Infrastructure.Postgres;
using Moq;
using Shouldly;
using Xunit;

namespace Jobs.Infrastructure.Tests;

public sealed class HangfireBggWriteQueueTests
{
	private readonly PostgresContainerFixture _postgres;
	private readonly Mock<IBackgroundJobClient> _jobs = new();

	public HangfireBggWriteQueueTests(PostgresContainerFixture postgres)
	{
		_postgres = postgres;
		_jobs.Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns("job-1");
	}

	[Fact]
	[Trait("AC", "111UC1")]
	public async Task QueueAddAsync_NewGame_InsertsOneAddRequestAndCreatesOneJobCarryingOnlyTheRequestId()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var gameId = Guid.NewGuid();

		await Queue(database).QueueAddAsync(gameId, 230802, "Azul", TestContext.Current.CancellationToken);

		var request = (await database.RequestsAsync()).ShouldHaveSingleItem();
		request.Kind.ShouldBe("add");
		request.BggId.ShouldBe(230802);
		request.GameId.ShouldBe(gameId);
		request.Name.ShouldBe("Azul");
		request.CollectionItemId.ShouldBeNull();
		request.Account.ShouldBe("owner");
		request.IdempotencyKey.ShouldBe($"add:{gameId}");
		request.Status.ShouldBe("queued");
		request.JobId.ShouldBe("job-1");
		request.Attempts.ShouldBe(0);

		var (job, state) = CreatedJob();
		job.Type.ShouldBe(typeof(IBggAddJob));
		job.Method.Name.ShouldBe(nameof(IBggAddJob.RunAsync));
		job.Args.Count.ShouldBe(3);
		job.Args[0].ShouldBe(request.Id);
		job.Args[1].ShouldBeNull();
		job.Args[2].ShouldBe(CancellationToken.None);
		state.ShouldBeOfType<EnqueuedState>().Queue.ShouldBe("bgg");
	}

	[Fact]
	[Trait("AC", "111UC2")]
	public async Task QueueRemoveAsync_GameWithCollectionItemId_InsertsOneRemoveRequestHoldingIt()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var gameId = Guid.NewGuid();

		await Queue(database).QueueRemoveAsync(gameId, 230802, 777, TestContext.Current.CancellationToken);

		var request = (await database.RequestsAsync()).ShouldHaveSingleItem();
		request.Kind.ShouldBe("remove");
		request.BggId.ShouldBe(230802);
		request.GameId.ShouldBe(gameId);
		request.CollectionItemId.ShouldBe(777);
		request.IdempotencyKey.ShouldBe($"remove:{gameId}");
		request.Status.ShouldBe("queued");
		request.JobId.ShouldBe("job-1");

		var (job, state) = CreatedJob();
		job.Type.ShouldBe(typeof(IBggRemoveJob));
		job.Args.Count.ShouldBe(3);
		job.Args[0].ShouldBe(request.Id);
		job.Args[1].ShouldBeNull();
		state.ShouldBeOfType<EnqueuedState>().Queue.ShouldBe("bgg");
	}

	[Fact]
	[Trait("AC", "111UC2")]
	public async Task QueueRemoveAsync_GameWithoutCollectionItemId_InsertsARequestWithNull()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);

		await Queue(database).QueueRemoveAsync(Guid.NewGuid(), 230802, null, TestContext.Current.CancellationToken);

		(await database.RequestsAsync()).ShouldHaveSingleItem().CollectionItemId.ShouldBeNull();
	}

	[Fact]
	[Trait("AC", "111UC4")]
	public async Task QueueAddAsync_JobClientThrows_LeavesNoRequestAndRethrows()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		_jobs.Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>())).Throws(new InvalidOperationException("storage down"));

		await Should.ThrowAsync<InvalidOperationException>(
			() => Queue(database).QueueAddAsync(Guid.NewGuid(), 230802, "Azul", TestContext.Current.CancellationToken));

		(await database.RequestsAsync()).ShouldBeEmpty();
	}

	[Fact]
	[Trait("AC", "111UC4")]
	public async Task QueueRemoveAsync_JobClientThrows_LeavesNoRequestAndRethrows()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		_jobs.Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>())).Throws(new InvalidOperationException("storage down"));

		await Should.ThrowAsync<InvalidOperationException>(
			() => Queue(database).QueueRemoveAsync(Guid.NewGuid(), 230802, 777, TestContext.Current.CancellationToken));

		(await database.RequestsAsync()).ShouldBeEmpty();
	}

	[Fact]
	[Trait("AC", "111UC5")]
	public async Task QueueAddAsync_SameGameTwice_CreatesOneRequestAndOneJob()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var gameId = Guid.NewGuid();
		var queue = Queue(database);

		await queue.QueueAddAsync(gameId, 230802, "Azul", TestContext.Current.CancellationToken);
		await queue.QueueAddAsync(gameId, 230802, "Azul", TestContext.Current.CancellationToken);

		(await database.RequestsAsync()).ShouldHaveSingleItem();
		_jobs.Verify(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Once);
	}

	[Fact]
	[Trait("AC", "111UC5")]
	public async Task QueueAddAsync_AddThenRemoveOfTheSameGame_KeepsBothRequests()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var gameId = Guid.NewGuid();
		var queue = Queue(database);

		await queue.QueueAddAsync(gameId, 230802, "Azul", TestContext.Current.CancellationToken);
		await queue.QueueRemoveAsync(gameId, 230802, null, TestContext.Current.CancellationToken);

		(await database.RequestsAsync()).Select(r => r.Kind).ShouldBe(["add", "remove"]);
	}

	[Fact]
	[Trait("AC", "111UC4")]
	public async Task QueueAddAsync_RequestAbortedWhileTheJobFails_StillRemovesTheRequestRow()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		using var aborted = new CancellationTokenSource();
		_jobs.Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
			.Callback(() => aborted.Cancel())
			.Throws(new InvalidOperationException("storage down"));

		await Should.ThrowAsync<InvalidOperationException>(
			() => Queue(database).QueueAddAsync(Guid.NewGuid(), 230802, "Azul", aborted.Token));

		(await database.RequestsAsync()).ShouldBeEmpty();
	}

	[Fact]
	[Trait("AC", "111UC4")]
	public async Task QueueAddAsync_RecordingTheJobIdFails_DeletesTheJobAndTheRequestAndRethrows()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		_jobs.Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
			.Callback(() => database.ExecuteAsync("drop function set_bgg_write_request_job(uuid, text)").GetAwaiter().GetResult())
			.Returns("job-1");

		await Should.ThrowAsync<Exception>(
			() => Queue(database).QueueAddAsync(Guid.NewGuid(), 230802, "Azul", TestContext.Current.CancellationToken));

		(await database.RequestsAsync()).ShouldBeEmpty();
		_jobs.Verify(c => c.ChangeState("job-1", It.IsAny<IState>(), It.IsAny<string>()), Times.Once);
	}

	private HangfireBggWriteQueue Queue(JobsTestDatabase database) =>
		new(database.Repository, _jobs.Object, TimeProvider.System);

	private (Job Job, IState State) CreatedJob()
	{
		var invocation = _jobs.Invocations.Single(i => i.Method.Name == nameof(IBackgroundJobClient.Create));
		return ((Job)invocation.Arguments[0], (IState)invocation.Arguments[1]);
	}
}