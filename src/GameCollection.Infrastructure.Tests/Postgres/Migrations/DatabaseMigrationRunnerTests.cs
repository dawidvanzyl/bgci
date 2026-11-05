using GameCollection.Infrastructure.Postgres.Migrations;
using GameCollection.Testing.Postgres;
using Npgsql;
using Shouldly;
using Xunit;

namespace GameCollection.Infrastructure.Tests.Postgres.Migrations;

public sealed class DatabaseMigrationRunnerTests
{
	private const string _schemaJournal = "migration_schema_journal";
	private const string _functionJournal = "migration_function_journal";
	private const string _seedJournal = "migration_seed_journal";

	private static readonly MigrationScripts _embedded = MigrationScripts.LoadEmbedded();

	private readonly PostgresContainerFixture _postgres;

	public DatabaseMigrationRunnerTests(PostgresContainerFixture postgres)
	{
		_postgres = postgres;
	}

	[Fact]
	[Trait("AC", "63UC1")]
	public async Task RunAsync_EmptyDatabase_AppliesEmbeddedSchemaScriptsInOrderAndRecordsThem()
	{
		var database = await NewDatabaseAsync();

		await RunAsync(database, _embedded);

		var expected = _embedded.Schema.Select(script => script.Name).Order(StringComparer.Ordinal).ToList();
		expected.First().ShouldBe("0001_create_collected_games.sql");
		(await database.JournalAsync(_schemaJournal)).ShouldBe(expected);
	}

	[Fact]
	[Trait("AC", "63UC1")]
	public async Task RunAsync_ScriptsGivenOutOfOrder_AppliesThemInNameOrder()
	{
		var database = await NewDatabaseAsync();
		var scripts = _embedded with
		{
			Schema =
			[
				.. _embedded.Schema,
				Script("0003_alter_order_probe.sql", "alter table order_probe add column note text;"),
				Script("0002_create_order_probe.sql", "create table order_probe (id integer);")
			]
		};

		await RunAsync(database, scripts);

		(await database.JournalAsync(_schemaJournal)).ShouldBe(
			["0001_create_collected_games.sql", "0002_create_order_probe.sql", "0003_alter_order_probe.sql"]);
	}

	[Fact]
	[Trait("AC", "63UC1")]
	public async Task RunAsync_AllCategories_RunsSchemaThenFunctionsThenSeeds()
	{
		var database = await NewDatabaseAsync();
		var scripts = _embedded with
		{
			Schema = [.. _embedded.Schema, Script("0002_create_run_order_probe.sql", "create table run_order_probe (id bigint);")],
			Functions =
			[
				Script(
					"run_order_probe_count.sql",
					"""
					create or replace function run_order_probe_count() returns bigint
					language sql as $body$ select count(*) from run_order_probe $body$;
					""")
			],
			Seeds = [Script("run_order_probe_seed.sql", "insert into run_order_probe (id) select run_order_probe_count() + 1;")]
		};

		await RunAsync(database, scripts);

		(await database.QueryScalarAsync<long>("select id from run_order_probe")).ShouldBe(1);
	}

	[Fact]
	[Trait("AC", "63UC2")]
	public async Task RunAsync_EmptyDatabase_CreatesGameTableWithOrderedTextLists()
	{
		var database = await NewDatabaseAsync();

		await RunAsync(database, _embedded);

		var expected = new List<TestDatabase.ColumnInfo>
		{
			Column("added_at", "timestamp with time zone", "timestamptz", false),
			Column("artists", "ARRAY", "_text", false),
			Column("best_player_count_max", "integer", "int4", true),
			Column("best_player_count_min", "integer", "int4", true),
			Column("bgg_coll_id", "bigint", "int8", true),
			Column("bgg_id", "integer", "int4", true),
			Column("bgg_rating", "numeric", "numeric", true),
			Column("bgg_weight", "numeric", "numeric", true),
			Column("categories", "ARRAY", "_text", false),
			Column("cover_image_url", "text", "text", true),
			Column("description", "text", "text", true),
			Column("designers", "ARRAY", "_text", false),
			Column("id", "uuid", "uuid", false),
			Column("max_play_time", "integer", "int4", true),
			Column("max_players", "integer", "int4", true),
			Column("mechanics", "ARRAY", "_text", false),
			Column("min_age", "integer", "int4", true),
			Column("min_play_time", "integer", "int4", true),
			Column("min_players", "integer", "int4", true),
			Column("name", "text", "text", false),
			Column("parent_game_id", "uuid", "uuid", true),
			Column("play_time_minutes", "integer", "int4", true),
			Column("publishers", "ARRAY", "_text", false),
			Column("subdomains", "ARRAY", "_text", false),
			Column("updated_at", "timestamp with time zone", "timestamptz", false),
			Column("year", "integer", "int4", true)
		}.OrderBy(column => column.Name, StringComparer.Ordinal).ToList();
		(await database.ColumnsAsync("collected_games")).ShouldBe(expected);

		await database.InsertGamesAsync();
		(await database.QueryScalarAsync<string>(
			$"select array_to_string(categories, ',') from collected_games where id = '{TestDatabase.GameAId}'"))
			.ShouldBe("Zeta,Alpha,Mu");
	}

	[Fact]
	[Trait("AC", "63UC3")]
	public async Task RunAsync_RunAgainOnPopulatedDatabase_AppliesNoSchemaScriptAndKeepsGames()
	{
		var database = await NewDatabaseAsync();
		await RunAsync(database, _embedded);
		await database.InsertGamesAsync();
		var snapshot = await database.SnapshotGamesAsync();
		var journal = await database.JournalAsync(_schemaJournal);

		await RunAsync(database, _embedded);

		(await database.JournalAsync(_schemaJournal)).ShouldBe(journal);
		(await database.SnapshotGamesAsync()).ShouldBe(snapshot);
	}

	[Fact]
	[Trait("AC", "63UC4")]
	public async Task RunAsync_FunctionScriptChanged_DeploysChangedFunctionAndRecordsEachDeployment()
	{
		var database = await NewDatabaseAsync();

		await RunAsync(database, _embedded with { Functions = [ProbeAnswer(1)] });

		(await database.JournalCountAsync(_functionJournal, "probe_answer.sql")).ShouldBe(1);

		await RunAsync(database, _embedded with { Functions = [ProbeAnswer(2)] });

		(await database.QueryScalarAsync<int>("select probe_answer()")).ShouldBe(2);
		(await database.JournalCountAsync(_functionJournal, "probe_answer.sql")).ShouldBe(2);
	}

	[Fact]
	[Trait("AC", "63UC5")]
	public async Task RunAsync_FunctionScriptDeleted_DropsFunctionOnlyWhenVersionedDropScriptExists()
	{
		var database = await NewDatabaseAsync();
		var withFunction = _embedded with
		{
			Functions = [Script("probe_retired.sql", "create or replace function probe_retired() returns int language sql as $body$ select 1 $body$;")]
		};
		await RunAsync(database, withFunction);

		await RunAsync(database, _embedded);

		(await database.FunctionExistsAsync("probe_retired")).ShouldBeTrue();

		var withDrop = _embedded with
		{
			Schema = [.. _embedded.Schema, Script("0002_drop_probe_retired.sql", "drop function if exists probe_retired();")]
		};
		await RunAsync(database, withDrop);

		(await database.FunctionExistsAsync("probe_retired")).ShouldBeFalse();
	}

	[Fact]
	[Trait("AC", "63UC6")]
	public async Task RunAsync_SchemaScriptFails_ReportsScriptAndAppliesNothingLater()
	{
		var database = await NewDatabaseAsync();
		var scripts = _embedded with
		{
			Schema =
			[
				.. _embedded.Schema,
				Script("0002_create_failure_probe.sql", "create table failure_probe (id integer); select 1/0;"),
				Script("0003_create_after_failure.sql", "create table after_failure (id integer);")
			],
			Functions = [ProbeAnswer(1)]
		};

		var exception = await Should.ThrowAsync<DatabaseMigrationException>(() => RunAsync(database, scripts));

		exception.ScriptName.ShouldBe("0002_create_failure_probe.sql");
		exception.Message.ShouldContain("0002_create_failure_probe.sql");
		exception.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe("22012");
		(await database.JournalAsync(_schemaJournal)).ShouldBe(["0001_create_collected_games.sql"]);
		(await database.TableExistsAsync("failure_probe")).ShouldBeFalse();
		(await database.TableExistsAsync("after_failure")).ShouldBeFalse();
		(await database.FunctionExistsAsync("probe_answer")).ShouldBeFalse();
		(await database.JournalAsync(_functionJournal)).ShouldBeEmpty();
	}

	[Fact]
	[Trait("AC", "63UC6")]
	public async Task RunAsync_FunctionScriptFails_ReportsScriptAndRunsNoSeed()
	{
		var database = await NewDatabaseAsync();
		var scripts = _embedded with
		{
			Functions =
			[
				Script(
					"probe_broken.sql",
					"create or replace function probe_broken() returns int language sql as $body$ select id from missing_table $body$;")
			],
			Seeds = [Script("probe_seed.sql", "select 1;")]
		};

		var exception = await Should.ThrowAsync<DatabaseMigrationException>(() => RunAsync(database, scripts));

		exception.ScriptName.ShouldBe("probe_broken.sql");
		(await database.JournalCountAsync(_functionJournal, "probe_broken.sql")).ShouldBe(0);
		(await database.JournalCountAsync(_seedJournal, "probe_seed.sql")).ShouldBe(0);
	}

	[Fact]
	[Trait("AC", "63UC7")]
	public async Task RunAsync_SeedScriptRunTwice_KeepsOneRowAndRecordsEachApplication()
	{
		var database = await NewDatabaseAsync();
		var scripts = _embedded with
		{
			Schema = [.. _embedded.Schema, Script("0002_create_seed_probe.sql", "create table seed_probe (name text primary key);")],
			Seeds = [Script("seed_probe_alpha.sql", "insert into seed_probe (name) values ('alpha') on conflict do nothing;")]
		};

		await RunAsync(database, scripts);

		(await database.JournalCountAsync(_seedJournal, "seed_probe_alpha.sql")).ShouldBe(1);

		await RunAsync(database, scripts);

		(await database.CountAsync("seed_probe")).ShouldBe(1);
		(await database.JournalCountAsync(_seedJournal, "seed_probe_alpha.sql")).ShouldBe(2);
	}

	[Fact]
	[Trait("AC", "63UC7")]
	public async Task RunAsync_NoFunctionOrSeedScripts_Succeeds()
	{
		var database = await NewDatabaseAsync();

		await Should.NotThrowAsync(() => RunAsync(database, _embedded));
	}

	[Fact]
	[Trait("AC", "63UC8")]
	public async Task RunAsync_NewerSchemaScriptAdded_ChangesSchemaAndKeepsStoredGames()
	{
		var database = await NewDatabaseAsync();
		await RunAsync(database, _embedded);
		await database.InsertGamesAsync();
		var snapshot = await database.SnapshotGamesAsync();
		var newer = _embedded with
		{
			Schema =
			[
				.. _embedded.Schema,
				Script("0002_add_shelf_location.sql", "alter table collected_games add column shelf_location text;")
			]
		};

		await RunAsync(database, newer);

		(await database.ColumnsAsync("collected_games")).ShouldContain(column => column.Name == "shelf_location");
		(await database.JournalAsync(_schemaJournal)).Last().ShouldBe("0002_add_shelf_location.sql");
		(await database.SnapshotGamesAsync("shelf_location")).ShouldBe(snapshot);
	}

	private static MigrationScript Script(string name, string contents) => new(name, contents);

	private static MigrationScript ProbeAnswer(int value) =>
		Script("probe_answer.sql", $"create or replace function probe_answer() returns int language sql as $body$ select {value} $body$;");

	private static TestDatabase.ColumnInfo Column(string name, string dataType, string udtName, bool nullable) =>
		new(name, dataType, udtName, nullable ? "YES" : "NO");

	private static Task RunAsync(TestDatabase database, MigrationScripts scripts) =>
		new DatabaseMigrationRunner(database.ConnectionString, scripts).RunAsync();

	private async Task<TestDatabase> NewDatabaseAsync() => new(await _postgres.CreateDatabaseAsync());
}