using Dapper;
using GameCollection.Testing.Postgres;
using Jobs.Infrastructure.Postgres;
using Npgsql;
using Shared.Persistence.Migrations;

namespace Jobs.Infrastructure.Tests;

internal sealed class JobsTestDatabase
{
	private JobsTestDatabase(string connectionString)
	{
		ConnectionString = connectionString;
		Repository = new PostgresBggWriteRequestRepository(connectionString);
	}

	public string ConnectionString { get; }

	public PostgresBggWriteRequestRepository Repository { get; }

	public static async Task<JobsTestDatabase> CreateAsync(PostgresContainerFixture postgres)
	{
		var connectionString = await postgres.CreateDatabaseAsync();
		var scripts = MigrationScripts.LoadEmbedded(typeof(DependencyInjection).Assembly, "Jobs.Infrastructure.Postgres.Scripts.");
		await new DatabaseMigrationRunner(connectionString, scripts).RunAsync();
		return new JobsTestDatabase(connectionString);
	}

	public async Task<IReadOnlyList<RequestRow>> RequestsAsync()
	{
		await using var connection = new NpgsqlConnection(ConnectionString);
		var rows = await connection.QueryAsync<RequestRow>(new CommandDefinition(
			"""
			select id, kind, bgg_id as BggId, game_id as GameId, name, collection_item_id as CollectionItemId, account,
				idempotency_key as IdempotencyKey, status, job_id as JobId, attempts
			from bgg_write_requests
			order by created_at
			"""));
		return rows.ToList();
	}

	public async Task<IReadOnlyList<LogRow>> LogAsync()
	{
		await using var connection = new NpgsqlConnection(ConnectionString);
		var rows = await connection.QueryAsync<LogRow>(new CommandDefinition(
			"""
			select request_id as RequestId, job_type as JobType, account, cast(payload as text) as Payload, result, error, attempts,
				started_at as StartedAt, ended_at as EndedAt
			from bgg_job_log
			order by id
			"""));
		return rows.ToList();
	}

	public sealed record RequestRow(
		Guid Id,
		string Kind,
		int BggId,
		Guid GameId,
		string Name,
		long? CollectionItemId,
		string Account,
		string IdempotencyKey,
		string Status,
		string? JobId,
		int Attempts);

	public sealed record LogRow(
		Guid RequestId,
		string JobType,
		string Account,
		string Payload,
		string? Result,
		string? Error,
		int Attempts,
		DateTime StartedAt,
		DateTime EndedAt);
}