using GameCollection.Testing.Postgres;
using Jobs.Application.Models;
using Shouldly;
using System.Text.Json;
using Xunit;

namespace Jobs.Infrastructure.Tests;

public sealed class PostgresBggWriteRequestRepositoryTests
{
	private static readonly DateTimeOffset _createdAt = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

	private readonly PostgresContainerFixture _postgres;

	public PostgresBggWriteRequestRepositoryTests(PostgresContainerFixture postgres)
	{
		_postgres = postgres;
	}

	[Fact]
	[Trait("AC", "111UC5")]
	public async Task CreateAsync_SecondRequestWithTheSameKey_InsertsNothingAndReportsFalse()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var gameId = Guid.NewGuid();

		var first = await CreateAsync(database, gameId: gameId);
		var second = await CreateAsync(database, gameId: gameId);

		first.ShouldBeTrue();
		second.ShouldBeFalse();
		(await database.RequestsAsync()).ShouldHaveSingleItem();
	}

	[Fact]
	public async Task GetAsync_StoredRequest_ReturnsEveryValue()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var id = Guid.NewGuid();
		var gameId = Guid.NewGuid();
		await CreateAsync(database, id, gameId, BggWriteKind.Remove, collectionItemId: 777);

		var request = await database.Repository.GetAsync(id, TestContext.Current.CancellationToken);

		request.ShouldBe(new BggWriteRequest(id, BggWriteKind.Remove, 230802, gameId, "Azul", 777, "owner", BggWriteRequestStatus.Queued, 0));
	}

	[Fact]
	public async Task GetAsync_UnknownRequest_ReturnsNull()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);

		(await database.Repository.GetAsync(Guid.NewGuid(), TestContext.Current.CancellationToken)).ShouldBeNull();
	}

	[Fact]
	public async Task StartAttemptAsync_EachCall_ReturnsTheNewCount()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var id = Guid.NewGuid();
		await CreateAsync(database, id);

		var first = await database.Repository.StartAttemptAsync(id, _createdAt, TestContext.Current.CancellationToken);
		var second = await database.Repository.StartAttemptAsync(id, _createdAt, TestContext.Current.CancellationToken);

		first.ShouldBe(1);
		second.ShouldBe(2);
		(await database.RequestsAsync()).ShouldHaveSingleItem().Attempts.ShouldBe(2);
	}

	[Fact]
	public async Task StartAttemptAsync_UnknownRequest_ReturnsNull()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);

		(await database.Repository.StartAttemptAsync(Guid.NewGuid(), _createdAt, TestContext.Current.CancellationToken)).ShouldBeNull();
	}

	[Theory]
	[InlineData(BggWriteRequestStatus.Cancelled)]
	[InlineData(BggWriteRequestStatus.Sent)]
	[Trait("AC", "112UC3")]
	public async Task StartAttemptAsync_RequestCancelledOrSent_ReturnsNullAndCountsNothing(BggWriteRequestStatus status)
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var id = Guid.NewGuid();
		await CreateAsync(database, id);
		await CompleteAsync(database, id, status);

		var attempts = await database.Repository.StartAttemptAsync(id, _createdAt, TestContext.Current.CancellationToken);

		attempts.ShouldBeNull();
		(await database.RequestsAsync()).ShouldHaveSingleItem().Attempts.ShouldBe(0);
	}

	[Fact]
	public async Task StartAttemptAsync_FailedRequest_CountsTheAttempt()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var id = Guid.NewGuid();
		await CreateAsync(database, id);
		await CompleteAsync(database, id, BggWriteRequestStatus.Failed);

		(await database.Repository.StartAttemptAsync(id, _createdAt, TestContext.Current.CancellationToken)).ShouldBe(1);
	}

	[Fact]
	[Trait("AC", "112UC3")]
	public async Task CompleteAsync_RequestAlreadyCancelled_KeepsItCancelledAndStillWritesTheLogRow()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var id = Guid.NewGuid();
		await CreateAsync(database, id);
		await CompleteAsync(database, id, BggWriteRequestStatus.Cancelled);

		await CompleteAsync(database, id, BggWriteRequestStatus.Sent);

		(await database.RequestsAsync()).ShouldHaveSingleItem().Status.ShouldBe("cancelled");
		(await database.LogAsync()).Count.ShouldBe(2);
	}

	[Fact]
	[Trait("AC", "112UC3")]
	public async Task CancelUnfinishedAddAsync_QueuedAddWithAttempts_CancelsItReturnsItsAttemptsAndWritesOneLogRow()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var cancellationToken = TestContext.Current.CancellationToken;
		var id = Guid.NewGuid();
		var gameId = Guid.NewGuid();
		await CreateAsync(database, id, gameId);
		await database.Repository.StartAttemptAsync(id, _createdAt, cancellationToken);

		var attempts = await database.Repository.CancelUnfinishedAddAsync(gameId, "owner", "BGG add", _createdAt, cancellationToken);

		attempts.ShouldBe(1);
		var request = (await database.RequestsAsync()).ShouldHaveSingleItem();
		request.Status.ShouldBe("cancelled");
		var log = (await database.LogAsync()).ShouldHaveSingleItem();
		log.RequestId.ShouldBe(id);
		log.JobType.ShouldBe("BGG add");
		log.Account.ShouldBe("owner");
		log.Result.ShouldBe("cancelled by a remove");
		log.Error.ShouldBeNull();
		log.Attempts.ShouldBe(1);
		using var payload = JsonDocument.Parse(log.Payload);
		payload.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray()
			.ShouldBe(["account", "bggId", "collectionItemId", "gameId", "kind", "name", "requestId"]);
		payload.RootElement.GetProperty("requestId").GetGuid().ShouldBe(id);
		payload.RootElement.GetProperty("kind").GetString().ShouldBe("add");
		payload.RootElement.GetProperty("bggId").GetInt32().ShouldBe(230802);
		payload.RootElement.GetProperty("gameId").GetGuid().ShouldBe(gameId);
		payload.RootElement.GetProperty("name").GetString().ShouldBe("Azul");
	}

	[Theory]
	[InlineData(BggWriteRequestStatus.Sent)]
	[InlineData(BggWriteRequestStatus.Failed)]
	[InlineData(BggWriteRequestStatus.Cancelled)]
	[Trait("AC", "112UC3")]
	public async Task CancelUnfinishedAddAsync_AddNotQueued_ReturnsNullAndChangesNothing(BggWriteRequestStatus status)
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var id = Guid.NewGuid();
		var gameId = Guid.NewGuid();
		await CreateAsync(database, id, gameId);
		await CompleteAsync(database, id, status);
		var logRows = (await database.LogAsync()).Count;

		var attempts = await database.Repository.CancelUnfinishedAddAsync(gameId, "owner", "BGG add", _createdAt, TestContext.Current.CancellationToken);

		attempts.ShouldBeNull();
		(await database.LogAsync()).Count.ShouldBe(logRows);
	}

	[Fact]
	[Trait("AC", "112UC3")]
	public async Task CancelUnfinishedAddAsync_OnlyARemoveOfTheGame_ReturnsNull()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var gameId = Guid.NewGuid();
		await CreateAsync(database, gameId: gameId, kind: BggWriteKind.Remove);

		(await database.Repository.CancelUnfinishedAddAsync(gameId, "owner", "BGG add", _createdAt, TestContext.Current.CancellationToken)).ShouldBeNull();
	}

	[Theory]
	[InlineData(BggWriteRequestStatus.Queued, null, true)]
	[InlineData(BggWriteRequestStatus.Queued, "Enqueued", true)]
	[InlineData(BggWriteRequestStatus.Queued, "Scheduled", true)]
	[InlineData(BggWriteRequestStatus.Queued, "Processing", true)]
	[InlineData(BggWriteRequestStatus.Queued, "Failed", true)]
	[InlineData(BggWriteRequestStatus.Queued, "Deleted", false)]
	[InlineData(BggWriteRequestStatus.Queued, "Gone", false)]
	[InlineData(BggWriteRequestStatus.Failed, "Enqueued", true)]
	[InlineData(BggWriteRequestStatus.Failed, "Deleted", true)]
	[InlineData(BggWriteRequestStatus.Sent, "Succeeded", false)]
	[InlineData(BggWriteRequestStatus.Cancelled, "Enqueued", false)]
	[Trait("AC", "112UC1")]
	public async Task HasUnresolvedRemoveAsync_RemoveInTheGivenState_ReportsWhetherItIsUnresolved(
		BggWriteRequestStatus status,
		string? jobState,
		bool expected)
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var id = Guid.NewGuid();
		await CreateAsync(database, id, kind: BggWriteKind.Remove);
		await SetUpJobAsync(database, id, 101, jobState);
		if (status != BggWriteRequestStatus.Queued)
			await CompleteAsync(database, id, status);

		var unresolved = await database.Repository.HasUnresolvedRemoveAsync("owner", 230802, TestContext.Current.CancellationToken);

		unresolved.ShouldBe(expected);
	}

	[Fact]
	[Trait("AC", "112UC1")]
	public async Task HasUnresolvedRemoveAsync_QueuedRemoveBeingRetried_ReportsUnresolved()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var id = Guid.NewGuid();
		await CreateAsync(database, id, kind: BggWriteKind.Remove);
		await SetUpJobAsync(database, id, 101, "Scheduled");
		await database.Repository.StartAttemptAsync(id, _createdAt, TestContext.Current.CancellationToken);

		(await database.Repository.HasUnresolvedRemoveAsync("owner", 230802, TestContext.Current.CancellationToken)).ShouldBeTrue();
	}

	[Fact]
	[Trait("AC", "112UC1")]
	public async Task HasUnresolvedRemoveAsync_DeletedJobMovedBackToEnqueued_ReportsUnresolvedAgain()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var id = Guid.NewGuid();
		await CreateAsync(database, id, kind: BggWriteKind.Remove);
		await SetUpJobAsync(database, id, 101, "Deleted");
		var cancellationToken = TestContext.Current.CancellationToken;
		(await database.Repository.HasUnresolvedRemoveAsync("owner", 230802, cancellationToken)).ShouldBeFalse();

		await database.SetHangfireJobStateAsync(101, "Enqueued");

		(await database.Repository.HasUnresolvedRemoveAsync("owner", 230802, cancellationToken)).ShouldBeTrue();
	}

	[Fact]
	[Trait("AC", "112UC1")]
	public async Task HasUnresolvedRemoveAsync_UnfinishedRequestsThatDoNotMatch_ReportsResolved()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		await CreateAsync(database, kind: BggWriteKind.Remove, bggId: 1);
		await CreateAsync(database, kind: BggWriteKind.Remove, account: "test");
		await CreateAsync(database, kind: BggWriteKind.Add);

		(await database.Repository.HasUnresolvedRemoveAsync("owner", 230802, TestContext.Current.CancellationToken)).ShouldBeFalse();
	}

	[Fact]
	[Trait("AC", "112UC2")]
	public async Task HasUnresolvedRemoveAsync_RemoveSent_ReportsResolved()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var id = Guid.NewGuid();
		await CreateAsync(database, id, kind: BggWriteKind.Remove);
		await CompleteAsync(database, id, BggWriteRequestStatus.Sent);

		(await database.Repository.HasUnresolvedRemoveAsync("owner", 230802, TestContext.Current.CancellationToken)).ShouldBeFalse();
	}

	[Theory]
	[InlineData(BggWriteRequestStatus.Queued, null, true)]
	[InlineData(BggWriteRequestStatus.Queued, "Enqueued", true)]
	[InlineData(BggWriteRequestStatus.Queued, "Scheduled", true)]
	[InlineData(BggWriteRequestStatus.Queued, "Deleted", false)]
	[InlineData(BggWriteRequestStatus.Queued, "Gone", false)]
	[InlineData(BggWriteRequestStatus.Sent, "Succeeded", false)]
	[InlineData(BggWriteRequestStatus.Failed, "Enqueued", false)]
	[InlineData(BggWriteRequestStatus.Cancelled, "Enqueued", false)]
	[InlineData(BggWriteRequestStatus.Cancelled, "Processing", true)]
	[InlineData(BggWriteRequestStatus.Cancelled, "Succeeded", false)]
	[InlineData(BggWriteRequestStatus.Queued, "Failed", false)]
	[Trait("AC", "112UC6")]
	public async Task HasUnfinishedEarlierRequestAsync_EarlierRequestInTheGivenState_ReportsWhetherItBlocks(
		BggWriteRequestStatus status,
		string? jobState,
		bool expected)
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var earlier = Guid.NewGuid();
		var later = Guid.NewGuid();
		await CreateAsync(database, earlier);
		await SetUpJobAsync(database, earlier, 101, jobState);
		if (status != BggWriteRequestStatus.Queued)
			await CompleteAsync(database, earlier, status);
		await CreateAsync(database, later, kind: BggWriteKind.Remove);

		var blocked = await database.Repository.HasUnfinishedEarlierRequestAsync(later, TestContext.Current.CancellationToken);

		blocked.ShouldBe(expected);
	}

	[Fact]
	[Trait("AC", "112UC6")]
	public async Task HasUnfinishedEarlierRequestAsync_LaterRequestOfTheSameGame_DoesNotBlockTheEarlierOne()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var earlier = Guid.NewGuid();
		await CreateAsync(database, earlier);
		await CreateAsync(database, kind: BggWriteKind.Remove);

		(await database.Repository.HasUnfinishedEarlierRequestAsync(earlier, TestContext.Current.CancellationToken)).ShouldBeFalse();
	}

	[Fact]
	[Trait("AC", "112UC6")]
	public async Task HasUnfinishedEarlierRequestAsync_EarlierRequestOfAnotherBggIdOrAccount_DoesNotBlock()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		await CreateAsync(database, bggId: 1);
		await CreateAsync(database, account: "test");
		var later = Guid.NewGuid();
		await CreateAsync(database, later, kind: BggWriteKind.Remove);

		(await database.Repository.HasUnfinishedEarlierRequestAsync(later, TestContext.Current.CancellationToken)).ShouldBeFalse();
	}

	[Fact]
	[Trait("AC", "112UC6")]
	public async Task HasUnfinishedEarlierRequestAsync_DeletedJobMovedBackToEnqueued_BlocksAgain()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var earlier = Guid.NewGuid();
		var later = Guid.NewGuid();
		await CreateAsync(database, earlier);
		await SetUpJobAsync(database, earlier, 101, "Deleted");
		await CreateAsync(database, later, kind: BggWriteKind.Remove);
		var cancellationToken = TestContext.Current.CancellationToken;
		(await database.Repository.HasUnfinishedEarlierRequestAsync(later, cancellationToken)).ShouldBeFalse();

		await database.SetHangfireJobStateAsync(101, "Enqueued");

		(await database.Repository.HasUnfinishedEarlierRequestAsync(later, cancellationToken)).ShouldBeTrue();
	}

	[Fact]
	[Trait("AC", "112UC6")]
	public async Task HasUnfinishedEarlierRequestAsync_AddCancelledWhileItsAttemptRuns_BlocksTheRemoveUntilTheJobLeavesProcessing()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var cancellationToken = TestContext.Current.CancellationToken;
		var add = Guid.NewGuid();
		var remove = Guid.NewGuid();
		var gameId = Guid.NewGuid();
		await CreateAsync(database, add, gameId);
		await SetUpJobAsync(database, add, 101, "Processing");
		await database.Repository.StartAttemptAsync(add, _createdAt, cancellationToken);
		await database.Repository.CancelUnfinishedAddAsync(gameId, "owner", "BGG add", _createdAt, cancellationToken);
		await CreateAsync(database, remove, gameId, BggWriteKind.Remove);

		(await database.Repository.HasUnfinishedEarlierRequestAsync(remove, cancellationToken)).ShouldBeTrue();

		await database.SetHangfireJobStateAsync(101, "Succeeded");

		(await database.Repository.HasUnfinishedEarlierRequestAsync(remove, cancellationToken)).ShouldBeFalse();
	}

	[Fact]
	[Trait("AC", "112UC6")]
	public async Task HasUnfinishedEarlierRequestAsync_RequestsCreatedAtTheSameInstant_OrdersThemBySequence()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var first = Guid.NewGuid();
		var second = Guid.NewGuid();
		await CreateAsync(database, first);
		await CreateAsync(database, second, kind: BggWriteKind.Remove);
		var cancellationToken = TestContext.Current.CancellationToken;

		(await database.Repository.HasUnfinishedEarlierRequestAsync(second, cancellationToken)).ShouldBeTrue();
		(await database.Repository.HasUnfinishedEarlierRequestAsync(first, cancellationToken)).ShouldBeFalse();
	}

	private static async Task SetUpJobAsync(JobsTestDatabase database, Guid requestId, long jobId, string? jobState)
	{
		if (jobState is null)
			return;

		await database.Repository.SetJobAsync(requestId, jobId.ToString(System.Globalization.CultureInfo.InvariantCulture), TestContext.Current.CancellationToken);
		if (jobState != "Gone")
			await database.InsertHangfireJobAsync(jobId, jobState);
	}

	[Fact]
	[Trait("AC", "111UC11")]
	public async Task CompleteAsync_Success_SetsTheStatusAndWritesExactlyOneLogRowWithEveryColumn()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var id = Guid.NewGuid();
		await CreateAsync(database, id);
		var started = _createdAt.AddSeconds(5);
		var ended = _createdAt.AddSeconds(8);

		await database.Repository.CompleteAsync(
			new BggWriteCompletion(id, BggWriteRequestStatus.Sent, 555, "BGG add", "owner", """{"requestId":"x","kind":"add"}""", "added", null, 2, started, ended),
			TestContext.Current.CancellationToken);

		var request = (await database.RequestsAsync()).ShouldHaveSingleItem();
		request.Status.ShouldBe("sent");
		request.CollectionItemId.ShouldBe(555);
		var log = (await database.LogAsync()).ShouldHaveSingleItem();
		log.RequestId.ShouldBe(id);
		log.JobType.ShouldBe("BGG add");
		log.Account.ShouldBe("owner");
		log.Payload.ShouldContain("\"kind\": \"add\"");
		log.Result.ShouldBe("added");
		log.Error.ShouldBeNull();
		log.Attempts.ShouldBe(2);
		log.StartedAt.ShouldBe(started.UtcDateTime);
		log.EndedAt.ShouldBe(ended.UtcDateTime);
	}

	[Fact]
	[Trait("AC", "111UC11")]
	public async Task CompleteAsync_Failure_SetsTheStatusFailedKeepsTheCollectionItemIdAndLogsTheError()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var id = Guid.NewGuid();
		await CreateAsync(database, id, kind: BggWriteKind.Remove, collectionItemId: 777);

		await database.Repository.CompleteAsync(
			new BggWriteCompletion(id, BggWriteRequestStatus.Failed, null, "BGG remove", "owner", "{}", null, "BGG down", 3, _createdAt, _createdAt),
			TestContext.Current.CancellationToken);

		var request = (await database.RequestsAsync()).ShouldHaveSingleItem();
		request.Status.ShouldBe("failed");
		request.CollectionItemId.ShouldBe(777);
		var log = (await database.LogAsync()).ShouldHaveSingleItem();
		log.Error.ShouldBe("BGG down");
		log.Result.ShouldBeNull();
	}

	[Fact]
	public async Task DeleteAsync_StoredRequest_RemovesIt()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var id = Guid.NewGuid();
		await CreateAsync(database, id);

		await database.Repository.DeleteAsync(id, TestContext.Current.CancellationToken);

		(await database.RequestsAsync()).ShouldBeEmpty();
	}

	[Fact]
	[Trait("AC", "111UC16")]
	public async Task ResetAsync_RequestsAndLogRecords_RemovesAllOfThem()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var id = Guid.NewGuid();
		await CreateAsync(database, id);
		await database.Repository.CompleteAsync(
			new BggWriteCompletion(id, BggWriteRequestStatus.Sent, 1, "BGG add", "owner", "{}", "added", null, 1, _createdAt, _createdAt),
			TestContext.Current.CancellationToken);

		await database.Repository.ResetAsync(TestContext.Current.CancellationToken);

		(await database.RequestsAsync()).ShouldBeEmpty();
		(await database.LogAsync()).ShouldBeEmpty();
		(await database.Repository.GetAsync(id, TestContext.Current.CancellationToken)).ShouldBeNull();
	}

	[Fact]
	[Trait("AC", "114UC2")]
	public async Task GetPendingRemoveBggIdsAsync_MixedRequests_ReturnsEachQueuedRemoveOfTheAccountOnce()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var cancellationToken = TestContext.Current.CancellationToken;
		var retried = Guid.NewGuid();
		var sent = Guid.NewGuid();
		var failed = Guid.NewGuid();
		await CreateAsync(database, kind: BggWriteKind.Remove, bggId: 1);
		await CreateAsync(database, kind: BggWriteKind.Remove, bggId: 1);
		await CreateAsync(database, retried, kind: BggWriteKind.Remove, bggId: 2);
		await database.Repository.StartAttemptAsync(retried, _createdAt, cancellationToken);
		await CreateAsync(database, sent, kind: BggWriteKind.Remove, bggId: 3);
		await CompleteAsync(database, sent, BggWriteRequestStatus.Sent);
		await CreateAsync(database, failed, kind: BggWriteKind.Remove, bggId: 4);
		await CompleteAsync(database, failed, BggWriteRequestStatus.Failed);
		await CreateAsync(database, kind: BggWriteKind.Add, bggId: 5);
		await CreateAsync(database, kind: BggWriteKind.Remove, bggId: 6, account: "test");

		var ids = await database.Repository.GetPendingRemoveBggIdsAsync("owner", cancellationToken);

		ids.OrderBy(id => id).ShouldBe([1, 2]);
	}

	private static Task CompleteAsync(JobsTestDatabase database, Guid id, BggWriteRequestStatus status) =>
		database.Repository.CompleteAsync(
			new BggWriteCompletion(id, status, null, "BGG remove", "owner", "{}", null, null, 1, _createdAt, _createdAt),
			TestContext.Current.CancellationToken);

	private static Task<bool> CreateAsync(
		JobsTestDatabase database,
		Guid? id = null,
		Guid? gameId = null,
		BggWriteKind kind = BggWriteKind.Add,
		long? collectionItemId = null,
		int bggId = 230802,
		string account = "owner")
	{
		var game = gameId ?? Guid.NewGuid();
		Guid? rowGame = account == "owner" ? game : null;
		return database.Repository.CreateAsync(
			id ?? Guid.NewGuid(),
			kind,
			bggId,
			rowGame,
			"Azul",
			collectionItemId,
			account,
			$"{kind.ToString().ToLowerInvariant()}:{game}",
			_createdAt,
			TestContext.Current.CancellationToken);
	}
}