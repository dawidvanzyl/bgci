using Dapper;
using GameCollection.Domain.Aggregates;
using GameCollection.Domain.Repositories;
using GameCollection.Domain.ValueObjects;
using Microsoft.Data.Sqlite;

namespace GameCollection.Infrastructure.Persistence;

public class SqliteCollectedGameRepository : ICollectedGameRepository
{
    private readonly string _connectionString;

    public SqliteCollectedGameRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private SqliteConnection CreateConnection() => new(_connectionString);

    public async Task<CollectedGame?> GetByIdAsync(GameId id, CancellationToken cancellationToken = default)
    {
        using var conn = CreateConnection();
        var row = await conn.QuerySingleOrDefaultAsync<GameRow>(
            "SELECT * FROM collected_games WHERE id = @Id",
            new { Id = id.Value.ToString() });

        return row is null ? null : MapToDomain(row);
    }

    public async Task<IReadOnlyList<CollectedGame>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync<GameRow>("SELECT * FROM collected_games ORDER BY added_at DESC");
        return rows.Select(MapToDomain).ToList().AsReadOnly();
    }

    public async Task AddAsync(CollectedGame game, CancellationToken cancellationToken = default)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("""
            INSERT INTO collected_games
                (id, name, year, description, min_players, max_players, play_time_minutes,
                 bgg_rating, cover_image_url, categories, mechanics, bgg_id, added_at, updated_at)
            VALUES
                (@Id, @Name, @Year, @Description, @MinPlayers, @MaxPlayers, @PlayTimeMinutes,
                 @BggRating, @CoverImageUrl, @Categories, @Mechanics, @BggId, @AddedAt, @UpdatedAt)
            """, MapToRow(game));
    }

    public async Task UpdateAsync(CollectedGame game, CancellationToken cancellationToken = default)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("""
            UPDATE collected_games SET
                name = @Name,
                year = @Year,
                description = @Description,
                min_players = @MinPlayers,
                max_players = @MaxPlayers,
                play_time_minutes = @PlayTimeMinutes,
                bgg_rating = @BggRating,
                cover_image_url = @CoverImageUrl,
                categories = @Categories,
                mechanics = @Mechanics,
                bgg_id = @BggId,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """, MapToRow(game));
    }

    public async Task DeleteAsync(GameId id, CancellationToken cancellationToken = default)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "DELETE FROM collected_games WHERE id = @Id",
            new { Id = id.Value.ToString() });
    }

    private static object MapToRow(CollectedGame game) => new
    {
        Id = game.Id.Value.ToString(),
        Name = game.Name.Value,
        game.Year,
        game.Description,
        MinPlayers = game.PlayerCount?.Min,
        MaxPlayers = game.PlayerCount?.Max,
        PlayTimeMinutes = game.PlayTime?.Minutes,
        BggRating = game.BggRating?.Value,
        CoverImageUrl = game.CoverImageUrl?.ToString(),
        Categories = string.Join("|", game.Categories),
        Mechanics = string.Join("|", game.Mechanics),
        BggId = game.BggId?.Value,
        game.AddedAt,
        game.UpdatedAt
    };

    private static CollectedGame MapToDomain(GameRow row)
    {
        var categories = string.IsNullOrEmpty(row.Categories)
            ? Array.Empty<string>()
            : row.Categories.Split('|', StringSplitOptions.RemoveEmptyEntries);

        var mechanics = string.IsNullOrEmpty(row.Mechanics)
            ? Array.Empty<string>()
            : row.Mechanics.Split('|', StringSplitOptions.RemoveEmptyEntries);

        return CollectedGame.Reconstitute(
            id: GameId.From(Guid.Parse(row.Id)),
            name: new GameName(row.Name),
            year: row.Year,
            description: row.Description,
            playerCount: row.MinPlayers.HasValue && row.MaxPlayers.HasValue
                ? new PlayerCount(row.MinPlayers.Value, row.MaxPlayers.Value)
                : null,
            playTime: row.PlayTimeMinutes.HasValue
                ? new PlayTime(row.PlayTimeMinutes.Value)
                : null,
            bggRating: row.BggRating.HasValue
                ? new BggRating(row.BggRating.Value)
                : null,
            coverImageUrl: row.CoverImageUrl is not null
                ? new Uri(row.CoverImageUrl)
                : null,
            categories: categories,
            mechanics: mechanics,
            bggId: row.BggId.HasValue
                ? BggGameId.From(row.BggId.Value)
                : null,
            addedAt: row.AddedAt,
            updatedAt: row.UpdatedAt
        );
    }

    private record GameRow(
        string Id,
        string Name,
        int? Year,
        string? Description,
        int? MinPlayers,
        int? MaxPlayers,
        int? PlayTimeMinutes,
        decimal? BggRating,
        string? CoverImageUrl,
        string? Categories,
        string? Mechanics,
        int? BggId,
        DateTime AddedAt,
        DateTime UpdatedAt
    );
}
