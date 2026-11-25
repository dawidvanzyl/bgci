using GameCollection.Testing.Postgres;
using Npgsql;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Api.Tests;

public sealed class GamesEndpointTests
{
	private readonly PostgresContainerFixture _postgres;

	public GamesEndpointTests(PostgresContainerFixture postgres)
	{
		_postgres = postgres;
	}

	[Fact]
	[Trait("AC", "111UC1")]
	public async Task PostFromBgg_ClientSendsSkipFlagAndCollId_IgnoresThemAnswersCreatedWithPendingAndTheListShowsIt()
	{
		await using var factory = await StartAsync();
		using var client = factory.CreateClient();

		var created = await client.PostAsJsonAsync("/api/games/from-bgg", new { name = "Azul", bggId = 230802, bggCollId = 5, skipBggWrite = true }, TestContext.Current.CancellationToken);

		created.StatusCode.ShouldBe(HttpStatusCode.Created);
		using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
		body.RootElement.GetProperty("bggStatus").GetString().ShouldBe("pending");
		var id = body.RootElement.GetProperty("id").GetGuid();

		using var list = JsonDocument.Parse(await client.GetStringAsync("/api/games", TestContext.Current.CancellationToken));
		var game = list.RootElement.EnumerateArray().Single(g => g.GetProperty("id").GetGuid() == id);
		game.GetProperty("bggStatus").GetString().ShouldBe("pending");
	}

	[Fact]
	[Trait("AC", "111UC2")]
	public async Task Delete_BggGame_AnswersNoContentAndAnUnknownGameAnswersNotFound()
	{
		await using var factory = await StartAsync();
		using var client = factory.CreateClient();
		var created = await client.PostAsJsonAsync("/api/games/from-bgg", new { name = "Azul", bggId = 230802 }, TestContext.Current.CancellationToken);
		using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
		var id = body.RootElement.GetProperty("id").GetGuid();

		var deleted = await client.DeleteAsync($"/api/games/{id}", TestContext.Current.CancellationToken);
		var deletedAgain = await client.DeleteAsync($"/api/games/{id}", TestContext.Current.CancellationToken);

		deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		deletedAgain.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	[Trait("AC", "112UC1")]
	public async Task PostFromBgg_RemoveOfTheBggGameNotFinished_AnswersConflictWithTheReasonCodeAndSavesNothing()
	{
		var database = await _postgres.CreateDatabaseAsync();
		var jobsDatabase = await _postgres.CreateDatabaseAsync();
		await using var factory = new ApiFactory(database, jobsDatabase);
		using var client = factory.CreateClient();
		await using (var connection = new NpgsqlConnection(jobsDatabase))
		{
			await connection.OpenAsync(TestContext.Current.CancellationToken);
			await using var insert = new NpgsqlCommand(
				"""
				insert into bgg_write_requests (id, kind, bgg_id, game_id, name, account, idempotency_key, status, attempts, created_at, updated_at)
				values (gen_random_uuid(), 'remove', 230802, gen_random_uuid(), '', 'owner', 'remove:held', 'queued', 0, now(), now())
				""",
				connection);
			await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
		}

		var refused = await client.PostAsJsonAsync("/api/games/from-bgg", new { name = "Azul", bggId = 230802 }, TestContext.Current.CancellationToken);

		refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		(await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("""{"reason":"bgg-remove-not-finished"}""");
		using var list = JsonDocument.Parse(await client.GetStringAsync("/api/games", TestContext.Current.CancellationToken));
		list.RootElement.GetArrayLength().ShouldBe(0);
	}

	private async Task<ApiFactory> StartAsync() =>
		new(await _postgres.CreateDatabaseAsync(), await _postgres.CreateDatabaseAsync());
}