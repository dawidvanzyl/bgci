using Dapper;
using Microsoft.Data.Sqlite;
namespace GameCollection.Infrastructure.Sqlite;

public class DatabaseMigrator
{
    private readonly string _connectionString;

    public DatabaseMigrator(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task MigrateAsync()
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS collected_games (
                id                TEXT PRIMARY KEY NOT NULL,
                name              TEXT NOT NULL,
                year              INTEGER,
                description       TEXT,
                min_players       INTEGER,
                max_players       INTEGER,
                play_time_minutes INTEGER,
                bgg_rating        REAL,
                cover_image_url   TEXT,
                categories        TEXT,
                mechanics         TEXT,
                bgg_id            INTEGER,
                bgg_coll_id       INTEGER,
                added_at          TEXT NOT NULL,
                updated_at        TEXT NOT NULL
            );
            """);

        // Add bgg_coll_id to databases created before this migration was introduced.
        // SQLite does not support ADD COLUMN IF NOT EXISTS; check via PRAGMA first.
        var columns = await conn.QueryAsync<string>("SELECT name FROM pragma_table_info('collected_games')");

        if (!columns.Contains("bgg_coll_id"))
        {
            await conn.ExecuteAsync("ALTER TABLE collected_games ADD COLUMN bgg_coll_id INTEGER;");
        }

        if (!columns.Contains("parent_game_id"))
        {
            await conn.ExecuteAsync("ALTER TABLE collected_games ADD COLUMN parent_game_id TEXT;");
        }

        if (!columns.Contains("bgg_weight"))
        {
            await conn.ExecuteAsync("ALTER TABLE collected_games ADD COLUMN bgg_weight REAL;");
        }

        if (!columns.Contains("min_age"))
        {
            await conn.ExecuteAsync("ALTER TABLE collected_games ADD COLUMN min_age INTEGER;");
        }

        if (!columns.Contains("min_play_time"))
        {
            await conn.ExecuteAsync("ALTER TABLE collected_games ADD COLUMN min_play_time INTEGER;");
        }

        if (!columns.Contains("max_play_time"))
        {
            await conn.ExecuteAsync("ALTER TABLE collected_games ADD COLUMN max_play_time INTEGER;");
        }

        if (!columns.Contains("designers"))
        {
            await conn.ExecuteAsync("ALTER TABLE collected_games ADD COLUMN designers TEXT;");
        }

        if (!columns.Contains("artists"))
        {
            await conn.ExecuteAsync("ALTER TABLE collected_games ADD COLUMN artists TEXT;");
        }

        if (!columns.Contains("publishers"))
        {
            await conn.ExecuteAsync("ALTER TABLE collected_games ADD COLUMN publishers TEXT;");
        }

        if (!columns.Contains("subdomains"))
        {
            await conn.ExecuteAsync("ALTER TABLE collected_games ADD COLUMN subdomains TEXT;");
        }
    }
}
