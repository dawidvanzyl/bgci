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
				g.id,
				g.name,
				g.year,
				g.description,
				g.min_players       AS MinPlayers,
				g.max_players       AS MaxPlayers,
				g.play_time_minutes AS PlayTimeMinutes,
				g.min_play_time     AS MinPlayTime,
				g.max_play_time     AS MaxPlayTime,
				g.bgg_rating        AS BggRating,
				g.bgg_weight        AS BggWeight,
				g.min_age           AS MinAge,
				g.best_player_count_min AS BestPlayerCountMin,
				g.best_player_count_max AS BestPlayerCountMax,
				g.cover_image_url   AS CoverImageUrl,
				g.categories,
				g.mechanics,
				g.designers,
				g.artists,
				g.publishers,
				g.subdomains,
				g.bgg_id            AS BggId,
				g.bgg_coll_id       AS BggCollId,
				g.parent_game_id    AS ParentGameId,
				(select count(*) from collected_games e where e.parent_game_id = g.id) AS ExpansionCount,
				g.added_at          AS AddedAt,
				g.updated_at        AS UpdatedAt
			from collected_games g
			where g.id = @Id
			""", new { Id = id.Value.ToString() });

        return row is null ? null : MapToDomain(row);
    }

    public async Task<IReadOnlyList<CollectedGame>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync<GameRow>("""
			select
				g.id,
				g.name,
				g.year,
				g.description,
				g.min_players       AS MinPlayers,
				g.max_players       AS MaxPlayers,
				g.play_time_minutes AS PlayTimeMinutes,
				g.min_play_time     AS MinPlayTime,
				g.max_play_time     AS MaxPlayTime,
				g.bgg_rating        AS BggRating,
				g.bgg_weight        AS BggWeight,
				g.min_age           AS MinAge,
				g.best_player_count_min AS BestPlayerCountMin,
				g.best_player_count_max AS BestPlayerCountMax,
				g.cover_image_url   AS CoverImageUrl,
				g.categories,
				g.mechanics,
				g.designers,
				g.artists,
				g.publishers,
				g.subdomains,
				g.bgg_id            AS BggId,
				g.bgg_coll_id       AS BggCollId,
				g.parent_game_id    AS ParentGameId,
				(select count(*) from collected_games e where e.parent_game_id = g.id) AS ExpansionCount,
				g.added_at          AS AddedAt,
				g.updated_at        AS UpdatedAt
			from collected_games g
			order by g.added_at desc
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
				min_play_time,
				max_play_time,
				bgg_rating,
				bgg_weight,
				min_age,
				best_player_count_min,
				best_player_count_max,
				cover_image_url,
				categories,
				mechanics,
				designers,
				artists,
				publishers,
				subdomains,
				bgg_id,
				bgg_coll_id,
				parent_game_id,
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
				@MinPlayTime,
				@MaxPlayTime,
				@BggRating,
				@BggWeight,
				@MinAge,
				@BestPlayerCountMin,
				@BestPlayerCountMax,
				@CoverImageUrl,
				@Categories,
				@Mechanics,
				@Designers,
				@Artists,
				@Publishers,
				@Subdomains,
				@BggId,
				@BggCollId,
				@ParentGameId,
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
				min_play_time = @MinPlayTime,
				max_play_time = @MaxPlayTime,
				bgg_rating = @BggRating,
				bgg_weight = @BggWeight,
				min_age = @MinAge,
				best_player_count_min = @BestPlayerCountMin,
				best_player_count_max = @BestPlayerCountMax,
				cover_image_url = @CoverImageUrl,
				categories = @Categories,
				mechanics = @Mechanics,
				designers = @Designers,
				artists = @Artists,
				publishers = @Publishers,
				subdomains = @Subdomains,
				bgg_id = @BggId,
				bgg_coll_id = @BggCollId,
				parent_game_id = @ParentGameId,
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
        MinPlayTime = game.MinPlayTimeMinutes,
        MaxPlayTime = game.MaxPlayTimeMinutes,
        BggRating = game.BggRating?.Value,
        BggWeight = game.BggWeight?.Value,
        MinAge = game.MinAge,
        BestPlayerCountMin = game.BestPlayerCountMin,
        BestPlayerCountMax = game.BestPlayerCountMax,
        CoverImageUrl = game.CoverImageUrl?.ToString(),
        Categories = string.Join("|", game.Categories),
        Mechanics = string.Join("|", game.Mechanics),
        Designers = string.Join("|", game.Designers),
        Artists = string.Join("|", game.Artists),
        Publishers = string.Join("|", game.Publishers),
        Subdomains = string.Join("|", game.Subdomains),
        BggId = game.BggId?.Value,
        game.BggCollId,
        ParentGameId = game.ParentGameId?.Value.ToString(),
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

        var designers = string.IsNullOrEmpty(row.Designers)
            ? Array.Empty<string>()
            : row.Designers.Split('|', StringSplitOptions.RemoveEmptyEntries);

        var artists = string.IsNullOrEmpty(row.Artists)
            ? Array.Empty<string>()
            : row.Artists.Split('|', StringSplitOptions.RemoveEmptyEntries);

        var publishers = string.IsNullOrEmpty(row.Publishers)
            ? Array.Empty<string>()
            : row.Publishers.Split('|', StringSplitOptions.RemoveEmptyEntries);

        var subdomains = string.IsNullOrEmpty(row.Subdomains)
            ? Array.Empty<string>()
            : row.Subdomains.Split('|', StringSplitOptions.RemoveEmptyEntries);

        var game = CollectedGame.Reconstitute(
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
            minPlayTimeMinutes: row.MinPlayTime,
            maxPlayTimeMinutes: row.MaxPlayTime,
            bggRating: row.BggRating.HasValue
                ? new BggRating(row.BggRating.Value)
                : null,
            bggWeight: row.BggWeight.HasValue
                ? new BggWeight(row.BggWeight.Value)
                : null,
            minAge: row.MinAge,
            bestPlayerCountMin: row.BestPlayerCountMin,
            bestPlayerCountMax: row.BestPlayerCountMax,
            coverImageUrl: row.CoverImageUrl is not null
                ? new Uri(row.CoverImageUrl)
                : null,
            categories: categories,
            mechanics: mechanics,
            designers: designers,
            artists: artists,
            publishers: publishers,
            subdomains: subdomains,
            bggId: row.BggId.HasValue
                ? BggGameId.From(row.BggId.Value)
                : null,
            bggCollId: row.BggCollId,
            parentGameId: row.ParentGameId is not null
                ? GameId.From(Guid.Parse(row.ParentGameId))
                : null,
            addedAt: row.AddedAt,
            updatedAt: row.UpdatedAt
        );

		game.SetExpansionCount(row.ExpansionCount);
        return game;
    }
}
