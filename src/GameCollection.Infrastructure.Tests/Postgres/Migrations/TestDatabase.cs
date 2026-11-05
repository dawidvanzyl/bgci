using Dapper;
using Npgsql;

namespace GameCollection.Infrastructure.Tests.Postgres.Migrations;

internal sealed class TestDatabase
{
	public static readonly Guid GameAId = Guid.Parse("11111111-1111-1111-1111-111111111111");
	public static readonly Guid GameBId = Guid.Parse("22222222-2222-2222-2222-222222222222");
	public static readonly Guid GameCId = Guid.Parse("33333333-3333-3333-3333-333333333333");

	private static readonly DateTime _addedAt = new(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
	private static readonly DateTime _updatedAt = new(2024, 6, 7, 8, 9, 10, DateTimeKind.Utc);

	public TestDatabase(string connectionString)
	{
		ConnectionString = connectionString;
	}

	public string ConnectionString { get; }

	public async Task InsertGamesAsync()
	{
		await using var connection = await OpenAsync();
		await connection.ExecuteAsync(
			"""
			insert into collected_games (
				id, name, year, description, min_players, max_players, play_time_minutes, min_play_time, max_play_time,
				bgg_rating, bgg_weight, min_age, best_player_count_min, best_player_count_max, cover_image_url,
				categories, mechanics, designers, artists, publishers, subdomains,
				bgg_id, bgg_coll_id, parent_game_id, added_at, updated_at)
			values (
				@id, @name, @year, @description, @minPlayers, @maxPlayers, @playTime, @minPlayTime, @maxPlayTime,
				@rating, @weight, @minAge, @bestMin, @bestMax, @cover,
				@categories, @mechanics, @designers, @artists, @publishers, @subdomains,
				@bggId, @bggCollId, @parentGameId, @addedAt, @updatedAt)
			""",
			new
			{
				id = GameAId,
				name = "Game A",
				year = 2019,
				description = "A game with every column set",
				minPlayers = 1,
				maxPlayers = 5,
				playTime = 90,
				minPlayTime = 60,
				maxPlayTime = 120,
				rating = 7.8312m,
				weight = 2.4567m,
				minAge = 12,
				bestMin = 2,
				bestMax = 4,
				cover = "https://example.test/a.png",
				categories = new[] { "Zeta", "Alpha", "Mu" },
				mechanics = new[] { "Card, Hand|Management", "Dice", "Auction" },
				designers = new[] { "Yves", "Anna", "Mark" },
				artists = new[] { "Zoe", "Bob", "Cy" },
				publishers = new[] { "Tau", "Pi", "Rho" },
				subdomains = new[] { "Strategy", "Family", "Abstract" },
				bggId = 4242,
				bggCollId = 9_000_000_001L,
				parentGameId = (Guid?)null,
				addedAt = _addedAt,
				updatedAt = _updatedAt
			});
		await connection.ExecuteAsync(
			"insert into collected_games (id, name, added_at, updated_at) values (@id, @name, @addedAt, @updatedAt)",
			new { id = GameBId, name = "Game B", addedAt = _addedAt, updatedAt = _updatedAt });
		await connection.ExecuteAsync(
			"insert into collected_games (id, name, parent_game_id, added_at, updated_at) values (@id, @name, @parent, @addedAt, @updatedAt)",
			new { id = GameCId, name = "Game C", parent = GameAId, addedAt = _addedAt, updatedAt = _updatedAt });
	}

	public async Task<IReadOnlyList<string>> SnapshotGamesAsync(params string[] excludedColumns)
	{
		await using var connection = await OpenAsync();
		var rows = await connection.QueryAsync<string>(
			"select (to_jsonb(g) - @excluded::text[])::text from collected_games g order by id",
			new { excluded = excludedColumns });
		return rows.ToList();
	}

	public async Task<IReadOnlyList<string>> JournalAsync(string table)
	{
		await using var connection = await OpenAsync();
		if (!await TableExistsAsync(connection, table))
		{
			return [];
		}

		var names = await connection.QueryAsync<string>($"select scriptname from {table} order by schemaversionsid");
		return names.ToList();
	}

	public async Task<int> JournalCountAsync(string table, string scriptName)
	{
		await using var connection = await OpenAsync();
		return !await TableExistsAsync(connection, table)
			? 0
			: await connection.ExecuteScalarAsync<int>(
			$"select count(*)::int from {table} where scriptname = @scriptName",
			new { scriptName });
	}

	public async Task<int> CountAsync(string table)
	{
		await using var connection = await OpenAsync();
		return await connection.ExecuteScalarAsync<int>($"select count(*)::int from {table}");
	}

	public async Task<bool> TableExistsAsync(string table)
	{
		await using var connection = await OpenAsync();
		return await TableExistsAsync(connection, table);
	}

	public async Task<bool> FunctionExistsAsync(string name)
	{
		await using var connection = await OpenAsync();
		return await connection.ExecuteScalarAsync<bool>(
			"select exists (select 1 from pg_proc where proname = @name)",
			new { name });
	}

	public async Task<T> QueryScalarAsync<T>(string sql)
	{
		await using var connection = await OpenAsync();
		return await connection.ExecuteScalarAsync<T>(sql) ?? throw new InvalidOperationException("The query returned null.");
	}

	public async Task<IReadOnlyList<ColumnInfo>> ColumnsAsync(string table)
	{
		await using var connection = await OpenAsync();
		var columns = await connection.QueryAsync<ColumnInfo>(
			"""
			select column_name as Name, data_type as DataType, udt_name as UdtName, is_nullable as IsNullable
			from information_schema.columns
			where table_schema = 'public' and table_name = @table
			order by column_name collate "C"
			""",
			new { table });
		return columns.ToList();
	}

	private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, string table) =>
		await connection.ExecuteScalarAsync<bool>("select to_regclass(@table) is not null", new { table = $"public.{table}" });

	private async Task<NpgsqlConnection> OpenAsync()
	{
		var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync();
		return connection;
	}

	public sealed record ColumnInfo(string Name, string DataType, string UdtName, string IsNullable);
}