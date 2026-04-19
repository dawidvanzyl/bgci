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
        // SQLite does not support ADD COLUMN IF NOT EXISTS, so we catch the exception silently.
        try
        {
            await conn.ExecuteAsync("ALTER TABLE collected_games ADD COLUMN bgg_coll_id INTEGER;");
        }
        catch (SqliteException)
        {
            // Column already exists — nothing to do.
        }
    }
}
