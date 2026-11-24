using Dapper;
using Jobs.Application.Abstractions;
using Jobs.Application.Models;
using Jobs.Infrastructure.Models;
using Npgsql;
using System.Data;

namespace Jobs.Infrastructure.Postgres;

public sealed class PostgresBggWriteRequestRepository : IBggWriteRequestRepository, IBggWriteRequestReset
{
	private readonly string _connectionString;

	public PostgresBggWriteRequestRepository(string connectionString)
	{
		_connectionString = connectionString;
	}

	public async Task<bool> CreateAsync(
		Guid id,
		BggWriteKind kind,
		int bggId,
		Guid gameId,
		string name,
		long? collectionItemId,
		string account,
		string idempotencyKey,
		DateTimeOffset createdAt,
		CancellationToken cancellationToken = default)
	{
		await using var connection = new NpgsqlConnection(_connectionString);
		var parameters = new DynamicParameters();
		parameters.Add("p_id", id);
		parameters.Add("p_kind", ToText(kind));
		parameters.Add("p_bgg_id", bggId);
		parameters.Add("p_game_id", gameId);
		parameters.Add("p_name", name);
		parameters.Add("p_collection_item_id", collectionItemId);
		parameters.Add("p_account", account);
		parameters.Add("p_idempotency_key", idempotencyKey);
		parameters.Add("p_created_at", createdAt);
		return await connection.ExecuteScalarAsync<bool>(Call("create_bgg_write_request", parameters, cancellationToken));
	}

	public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		await using var connection = new NpgsqlConnection(_connectionString);
		var parameters = new DynamicParameters();
		parameters.Add("p_id", id);
		await connection.ExecuteAsync(Call("delete_bgg_write_request", parameters, cancellationToken));
	}

	public async Task SetJobAsync(Guid id, string jobId, CancellationToken cancellationToken = default)
	{
		await using var connection = new NpgsqlConnection(_connectionString);
		var parameters = new DynamicParameters();
		parameters.Add("p_id", id);
		parameters.Add("p_job_id", jobId);
		await connection.ExecuteAsync(Call("set_bgg_write_request_job", parameters, cancellationToken));
	}

	public async Task<BggWriteRequest?> GetAsync(Guid requestId, CancellationToken cancellationToken = default)
	{
		await using var connection = new NpgsqlConnection(_connectionString);
		var parameters = new DynamicParameters();
		parameters.Add("p_id", requestId);
		var row = await connection.QuerySingleOrDefaultAsync<BggWriteRequestRow>(
			Call("get_bgg_write_request", parameters, cancellationToken));
		return row is null ? null : MapToModel(row);
	}

	public async Task<int> RecordAttemptAsync(Guid requestId, DateTimeOffset at, CancellationToken cancellationToken = default)
	{
		await using var connection = new NpgsqlConnection(_connectionString);
		var parameters = new DynamicParameters();
		parameters.Add("p_id", requestId);
		parameters.Add("p_at", at);
		return await connection.ExecuteScalarAsync<int?>(Call("record_bgg_write_attempt", parameters, cancellationToken))
			?? throw new InvalidOperationException($"BGG write request {requestId} no longer exists.");
	}

	public async Task CompleteAsync(BggWriteCompletion completion, CancellationToken cancellationToken = default)
	{
		await using var connection = new NpgsqlConnection(_connectionString);
		var parameters = new DynamicParameters();
		parameters.Add("p_id", completion.RequestId);
		parameters.Add("p_status", ToText(completion.Status));
		parameters.Add("p_collection_item_id", completion.CollectionItemId);
		parameters.Add("p_job_type", completion.JobType);
		parameters.Add("p_account", completion.Account);
		parameters.Add("p_payload", completion.Payload);
		parameters.Add("p_result", completion.Result);
		parameters.Add("p_error", completion.Error);
		parameters.Add("p_attempts", completion.Attempts);
		parameters.Add("p_started_at", completion.StartedAt);
		parameters.Add("p_ended_at", completion.EndedAt);
		await connection.ExecuteAsync(Call("complete_bgg_write_request", parameters, cancellationToken));
	}

	public async Task ResetAsync(CancellationToken cancellationToken = default)
	{
		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.ExecuteAsync(Call("reset_bgg_jobs", null, cancellationToken));
	}

	private static CommandDefinition Call(string function, DynamicParameters? parameters, CancellationToken cancellationToken) =>
		new(function, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken);

	private static string ToText(BggWriteKind kind) => kind switch
	{
		BggWriteKind.Add => "add",
		BggWriteKind.Remove => "remove",
		_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
	};

	private static string ToText(BggWriteRequestStatus status) => status switch
	{
		BggWriteRequestStatus.Queued => "queued",
		BggWriteRequestStatus.Sent => "sent",
		BggWriteRequestStatus.Failed => "failed",
		_ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
	};

	private static BggWriteRequest MapToModel(BggWriteRequestRow row) =>
		new(
			row.Id,
			row.Kind switch
			{
				"add" => BggWriteKind.Add,
				"remove" => BggWriteKind.Remove,
				_ => throw new InvalidOperationException($"Unknown BGG write kind '{row.Kind}'.")
			},
			row.BggId,
			row.GameId,
			row.Name,
			row.CollectionItemId,
			row.Account,
			row.Status switch
			{
				"queued" => BggWriteRequestStatus.Queued,
				"sent" => BggWriteRequestStatus.Sent,
				"failed" => BggWriteRequestStatus.Failed,
				_ => throw new InvalidOperationException($"Unknown BGG write status '{row.Status}'.")
			},
			row.Attempts);
}