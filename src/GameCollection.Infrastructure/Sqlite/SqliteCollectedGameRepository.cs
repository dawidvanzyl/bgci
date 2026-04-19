using Dapper;
using GameCollection.Domain.Aggregates;
using GameCollection.Domain.Repositories;
using GameCollection.Domain.ValueObjects;
using GameCollection.Infrastructure.Models;
using Microsoft.Data.Sqlite;

namespace GameCollection.Infrastructure.Sqlite;

public partial class SqliteCollectedGameRepository : ICollectedGameRepository
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
        var row = await conn.QuerySingleOrDefaultAsync<GameRow>("""
			select
				id,
				name,
				year,
				description,
				min_players       AS MinPlayers,
				max_players       AS MaxPlayers,
				play_time_minutes AS PlayTimeMinutes,
				bgg_rating        AS BggRating,
				cover_image_url   AS CoverImageUrl,
				categories,
				mechanics,
				bgg_id            AS BggId,
				bgg_coll_id       AS BggCollId,
				added_at          AS AddedAt,
				updated_at        AS UpdatedAt
			from collected_games
			where id = @Id
			""", new { Id = id.Value.ToString() });

        return row is null ? null : MapToDomain(row);
    }

    public async Task<IReadOnlyList<CollectedGame>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync<GameRow>("""
			select
				id,
				name,
				year,
				description,
				min_players       AS MinPlayers,
				max_players       AS MaxPlayers,
				play_time_minutes AS PlayTimeMinutes,
				bgg_rating        AS BggRating,
				cover_image_url   AS CoverImageUrl,
				categories,
				mechanics,
				bgg_id            AS BggId,
				bgg_coll_id       AS BggCollId,
				added_at          AS AddedAt,
				updated_at        AS UpdatedAt
			from collected_games
			order by added_at desc
			""");

        return rows
			.Select(MapToDomain)
			.ToList()
			.AsReadOnly();
    }

    public async Task AddAsync(CollectedGame game, CancellationToken cancellationToken = default)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("""
			insert into collected_games (
				id,
				name,
				year,
				description,
				min_players,
				max_players,
				play_time_minutes,
				bgg_rating,
				cover_image_url,
				categories,
				mechanics,
				bgg_id,
				bgg_coll_id,
				added_at,
				updated_at
			)
			values (
				@Id,
				@Name,
				@Year,
				@Description,
				@MinPlayers,
				@MaxPlayers,
				@PlayTimeMinutes,
				@BggRating,
				@CoverImageUrl,
				@Categories,
				@Mechanics,
				@BggId,
				@BggCollId,
				@AddedAt,
				@UpdatedAt
			)
			""", MapToRow(game));
    }

    public async Task UpdateAsync(CollectedGame game, CancellationToken cancellationToken = default)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("""
			update collected_games set
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
				bgg_coll_id = @BggCollId,
				updated_at = @UpdatedAt
			where id = @Id
			""", MapToRow(game));
    }

    public async Task DeleteAsync(GameId id, CancellationToken cancellationToken = default)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("""
			delete from collected_games
			where id = @Id
			""", new { Id = id.Value.ToString() });
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
        game.BggCollId,
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
            bggCollId: row.BggCollId,
            addedAt: row.AddedAt,
            updatedAt: row.UpdatedAt
        );
    }
}
