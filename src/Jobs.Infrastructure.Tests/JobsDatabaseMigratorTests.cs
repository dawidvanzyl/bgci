using GameCollection.Testing.Postgres;
using Hangfire;
using Hangfire.PostgreSql;
using Jobs.Infrastructure.Postgres;
using Npgsql;
using Shared.Persistence.Migrations;
using Shouldly;
using Xunit;

namespace Jobs.Infrastructure.Tests;

public sealed class JobsDatabaseMigratorTests
{
	private readonly PostgresContainerFixture _postgres;

	public JobsDatabaseMigratorTests(PostgresContainerFixture postgres)
	{
		_postgres = postgres;
	}

	[Fact]
	[Trait("AC", "111UC1")]
	public async Task WaitForHangfireSchemaAsync_NoJobsServiceInstallsIt_ReturnsFalseAfterTheLimit()
	{
		var connectionString = await _postgres.CreateDatabaseAsync();

		var found = await Migrator(connectionString).WaitForHangfireSchemaAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

		found.ShouldBeFalse();
	}

	[Fact]
	[Trait("AC", "111UC1")]
	public async Task WaitForHangfireSchemaAsync_InstallerHasOnlyPartlyFinished_ReturnsFalseAfterTheLimit()
	{
		var connectionString = await _postgres.CreateDatabaseAsync();
		await using (var connection = new NpgsqlConnection(connectionString))
		{
			await connection.OpenAsync(TestContext.Current.CancellationToken);
			await using var command = new NpgsqlCommand(
				"create schema hangfire; create table hangfire.job (id bigint); create table hangfire.schema (version int); insert into hangfire.schema values (3)",
				connection);
			await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
		}

		var found = await Migrator(connectionString).WaitForHangfireSchemaAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

		found.ShouldBeFalse();
	}

	[Fact]
	[Trait("AC", "111UC1")]
	public async Task WaitForHangfireSchemaAsync_JobsServiceInstallsItLater_ReturnsTrue()
	{
		var connectionString = await _postgres.CreateDatabaseAsync();
		var wait = Migrator(connectionString).WaitForHangfireSchemaAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

		await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
		GlobalConfiguration.Configuration.UsePostgreSqlStorage(
			options => options.UseNpgsqlConnection(connectionString),
			new PostgreSqlStorageOptions { PrepareSchemaIfNecessary = true, SchemaName = "hangfire" });

		(await wait).ShouldBeTrue();
	}

	[Fact]
	[Trait("AC", "112UC6")]
	public async Task RunAsync_NoHangfireSchemaYet_CreatesEveryFunctionAndFinishes()
	{
		var connectionString = await _postgres.CreateDatabaseAsync();
		var scripts = MigrationScripts.LoadEmbedded(typeof(DependencyInjection).Assembly, "Jobs.Infrastructure.Postgres.Scripts.");

		await new DatabaseMigrationRunner(connectionString, scripts).RunAsync(TestContext.Current.CancellationToken);

		var functions = await QueryFunctionNamesAsync(connectionString);
		functions.ShouldContain("has_unfinished_earlier_bgg_write_request");
		functions.ShouldContain("has_unresolved_bgg_remove");
		functions.ShouldContain("start_bgg_write_attempt");
		functions.ShouldContain("cancel_unfinished_bgg_add");
	}

	[Fact]
	[Trait("AC", "112UC3")]
	public async Task RunAsync_RowsFromTheFirstTwoScripts_GetASequenceInCreationOrderAcceptCancelledAndLoseTheOldAttemptFunction()
	{
		var connectionString = await _postgres.CreateDatabaseAsync();
		var all = MigrationScripts.LoadEmbedded(typeof(DependencyInjection).Assembly, "Jobs.Infrastructure.Postgres.Scripts.");
		var firstTwo = new MigrationScripts(all.Schema.Where(s => s.Name.StartsWith("0001", StringComparison.Ordinal) || s.Name.StartsWith("0002", StringComparison.Ordinal)).ToList(), [], []);
		await new DatabaseMigrationRunner(connectionString, firstTwo).RunAsync(TestContext.Current.CancellationToken);
		await using (var connection = new NpgsqlConnection(connectionString))
		{
			await connection.OpenAsync(TestContext.Current.CancellationToken);
			await using var command = new NpgsqlCommand(
				"""
				insert into bgg_write_requests (id, kind, bgg_id, game_id, name, account, idempotency_key, status, attempts, created_at, updated_at)
				values (gen_random_uuid(), 'add', 1, gen_random_uuid(), 'Azul', 'owner', 'add:later', 'queued', 0, now() + interval '1 hour', now()),
					(gen_random_uuid(), 'add', 1, gen_random_uuid(), 'Azul', 'owner', 'add:earlier', 'queued', 0, now(), now());
				create function record_bgg_write_attempt(p_id uuid, p_at timestamptz) returns integer language sql as 'select 1';
				""",
				connection);
			await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
		}

		await new DatabaseMigrationRunner(connectionString, all).RunAsync(TestContext.Current.CancellationToken);

		await using var verify = new NpgsqlConnection(connectionString);
		await verify.OpenAsync(TestContext.Current.CancellationToken);
		await using var check = new NpgsqlCommand(
			"""
			update bgg_write_requests set status = 'cancelled';
			select (select count(*) from bgg_write_requests where seq is not null)
				+ 10 * (select count(*) from pg_proc where proname = 'record_bgg_write_attempt')
				+ 100 * (select count(*) from bgg_write_requests e, bgg_write_requests l
					where e.idempotency_key = 'add:earlier' and l.idempotency_key = 'add:later' and e.seq < l.seq);
			""",
			verify);
		Convert.ToInt32(await check.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture).ShouldBe(102);
	}

	private static async Task<List<string>> QueryFunctionNamesAsync(string connectionString)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken);
		await using var command = new NpgsqlCommand("select proname from pg_proc", connection);
		await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
		var names = new List<string>();
		while (await reader.ReadAsync(TestContext.Current.CancellationToken))
		{
			names.Add(reader.GetString(0));
		}

		return names;
	}

	private static JobsDatabaseMigrator Migrator(string connectionString) =>
		new(connectionString, new DatabaseMigrationRunner(connectionString, new MigrationScripts([], [], [])));
}