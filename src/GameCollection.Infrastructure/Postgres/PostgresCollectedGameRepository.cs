using Dapper;
using GameCollection.Domain.Aggregates;
using GameCollection.Domain.Repositories;
using GameCollection.Domain.ValueObjects;
using GameCollection.Infrastructure.Models;
using Npgsql;
using System.Data;

namespace GameCollection.Infrastructure.Postgres;

public sealed class PostgresCollectedGameRepository : ICollectedGameRepository
{
	private readonly string _connectionString;

	public PostgresCollectedGameRepository(string connectionString)
	{
		_connectionString = connectionString;
	}

	public async Task<CollectedGame?> GetByIdAsync(GameId id, CancellationToken cancellationToken = default)
	{
		await using var connection = new NpgsqlConnection(_connectionString);
		var parameters = new DynamicParameters();
		parameters.Add("p_id", id.Value);
		var row = await connection.QuerySingleOrDefaultAsync<GameRow>(Call("get_collected_game_by_id", parameters, cancellationToken));
		return row is null ? null : MapToDomain(row);
	}

	public async Task<IReadOnlyList<CollectedGame>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		await using var connection = new NpgsqlConnection(_connectionString);
		var rows = await connection.QueryAsync<GameRow>(Call("get_collected_games", null, cancellationToken));
		return rows.Select(MapToDomain).ToList().AsReadOnly();
	}

	public async Task AddAsync(CollectedGame game, CancellationToken cancellationToken = default)
	{
		await using var connection = new NpgsqlConnection(_connectionString);
		var parameters = GameParameters(game);
		parameters.Add("p_added_at", game.AddedAt);
		parameters.Add("p_updated_at", game.UpdatedAt);
		await connection.ExecuteAsync(Call("create_collected_game", parameters, cancellationToken));
	}

	public async Task UpdateAsync(CollectedGame game, CancellationToken cancellationToken = default)
	{
		await using var connection = new NpgsqlConnection(_connectionString);
		var parameters = GameParameters(game);
		parameters.Add("p_updated_at", game.UpdatedAt);
		await connection.ExecuteAsync(Call("update_collected_game", parameters, cancellationToken));
	}

	public async Task DeleteAsync(GameId id, CancellationToken cancellationToken = default)
	{
		await using var connection = new NpgsqlConnection(_connectionString);
		var parameters = new DynamicParameters();
		parameters.Add("p_id", id.Value);
		await connection.ExecuteAsync(Call("delete_collected_game", parameters, cancellationToken));
	}

	private static CommandDefinition Call(string function, DynamicParameters? parameters, CancellationToken cancellationToken) =>
		new(function, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken);

	private static DynamicParameters GameParameters(CollectedGame game)
	{
		var parameters = new DynamicParameters();
		parameters.Add("p_id", game.Id.Value);
		parameters.Add("p_name", game.Name.Value);
		parameters.Add("p_year", game.Year);
		parameters.Add("p_description", game.Description);
		parameters.Add("p_min_players", game.PlayerCount?.Min);
		parameters.Add("p_max_players", game.PlayerCount?.Max);
		parameters.Add("p_play_time_minutes", game.PlayTime?.Minutes);
		parameters.Add("p_min_play_time", game.MinPlayTimeMinutes);
		parameters.Add("p_max_play_time", game.MaxPlayTimeMinutes);
		parameters.Add("p_bgg_rating", game.BggRating?.Value);
		parameters.Add("p_bgg_weight", game.BggWeight?.Value);
		parameters.Add("p_min_age", game.MinAge);
		parameters.Add("p_best_player_count_min", game.BestPlayerCountMin);
		parameters.Add("p_best_player_count_max", game.BestPlayerCountMax);
		parameters.Add("p_cover_image_url", game.CoverImageUrl?.ToString());
		parameters.Add("p_categories", game.Categories.ToArray());
		parameters.Add("p_mechanics", game.Mechanics.ToArray());
		parameters.Add("p_designers", game.Designers.ToArray());
		parameters.Add("p_artists", game.Artists.ToArray());
		parameters.Add("p_publishers", game.Publishers.ToArray());
		parameters.Add("p_subdomains", game.Subdomains.ToArray());
		parameters.Add("p_bgg_id", game.BggId?.Value);
		parameters.Add("p_bgg_coll_id", game.BggCollId);
		parameters.Add("p_parent_game_id", game.ParentGameId?.Value);
		return parameters;
	}

	private static CollectedGame MapToDomain(GameRow row)
	{
		var game = CollectedGame.Reconstitute(
			id: GameId.From(row.Id),
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
			categories: row.Categories,
			mechanics: row.Mechanics,
			designers: row.Designers,
			artists: row.Artists,
			publishers: row.Publishers,
			subdomains: row.Subdomains,
			bggId: row.BggId.HasValue
				? BggGameId.From(row.BggId.Value)
				: null,
			bggCollId: row.BggCollId,
			parentGameId: row.ParentGameId.HasValue
				? GameId.From(row.ParentGameId.Value)
				: null,
			addedAt: row.AddedAt,
			updatedAt: row.UpdatedAt
		);

		game.SetExpansionCount(row.ExpansionCount);
		return game;
	}
}