using Npgsql;
using Shared.Persistence.Migrations;

namespace Jobs.Infrastructure.Postgres;

public sealed class JobsDatabaseMigrator
{
	private static readonly TimeSpan _schemaPollInterval = TimeSpan.FromMilliseconds(500);

	private readonly string _connectionString;
	private readonly DatabaseMigrationRunner _runner;

	public JobsDatabaseMigrator(string connectionString, DatabaseMigrationRunner runner)
	{
		_connectionString = connectionString;
		_runner = runner;
	}

	public Task RunAsync(CancellationToken cancellationToken = default) => _runner.RunAsync(cancellationToken);

	/// <summary>
	/// The jobs service alone installs the Hangfire schema, because Hangfire.PostgreSql takes no lock
	/// while it installs. The API waits here until that schema exists, so its first enqueue cannot fail.
	/// </summary>
	public async Task<bool> WaitForHangfireSchemaAsync(TimeSpan limit, CancellationToken cancellationToken = default)
	{
		using var timeLimit = new CancellationTokenSource(limit);
		using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeLimit.Token);
		try
		{
			while (!await HangfireSchemaExistsAsync(linked.Token))
			{
				await Task.Delay(_schemaPollInterval, linked.Token);
			}

			return true;
		}
		catch (OperationCanceledException) when (timeLimit.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			return false;
		}
	}

	private async Task<bool> HangfireSchemaExistsAsync(CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);
		await using var command = new NpgsqlCommand("select to_regclass('hangfire.job') is not null", connection);
		return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
	}
}