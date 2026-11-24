using GameCollection.Testing.Postgres;
using Jobs.Application.Models;
using Shouldly;
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
	public async Task RecordAttemptAsync_EachCall_ReturnsTheNewCount()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);
		var id = Guid.NewGuid();
		await CreateAsync(database, id);

		var first = await database.Repository.RecordAttemptAsync(id, _createdAt, TestContext.Current.CancellationToken);
		var second = await database.Repository.RecordAttemptAsync(id, _createdAt, TestContext.Current.CancellationToken);

		first.ShouldBe(1);
		second.ShouldBe(2);
		(await database.RequestsAsync()).ShouldHaveSingleItem().Attempts.ShouldBe(2);
	}

	[Fact]
	public async Task RecordAttemptAsync_UnknownRequest_Throws()
	{
		var database = await JobsTestDatabase.CreateAsync(_postgres);

		await Should.ThrowAsync<InvalidOperationException>(
			() => database.Repository.RecordAttemptAsync(Guid.NewGuid(), _createdAt, TestContext.Current.CancellationToken));
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
		await database.Repository.RecordAttemptAsync(retried, _createdAt, cancellationToken);
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
		return database.Repository.CreateAsync(
			id ?? Guid.NewGuid(),
			kind,
			bggId,
			game,
			"Azul",
			collectionItemId,
			account,
			$"{kind.ToString().ToLowerInvariant()}:{game}",
			_createdAt,
			TestContext.Current.CancellationToken);
	}
}