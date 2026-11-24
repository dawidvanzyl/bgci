using Hangfire.PostgreSql;
using Npgsql;
using Shared.Persistence.Migrations;
using System.Text.RegularExpressions;

namespace Jobs.Infrastructure.Postgres;

public sealed class JobsDatabaseMigrator
{
	private static readonly TimeSpan _schemaPollInterval = TimeSpan.FromMilliseconds(500);
	private static readonly int _latestSchemaVersion = LatestHangfireSchemaVersion();

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
	/// while it installs. The API waits here until the installer has recorded its latest schema version, so its first enqueue cannot fail.
	/// </summary>
	public async Task<bool> WaitForHangfireSchemaAsync(TimeSpan limit, CancellationToken cancellationToken = default)
	{
		using var timeLimit = new CancellationTokenSource(limit);
		using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeLimit.Token);
		try
		{
			while (!await HangfireSchemaIsCurrentAsync(linked.Token))
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

	private async Task<bool> HangfireSchemaIsCurrentAsync(CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);
		await using var exists = new NpgsqlCommand("select to_regclass('hangfire.schema') is not null", connection);
		if (!(bool)(await exists.ExecuteScalarAsync(cancellationToken))!)
		{
			return false;
		}

		await using var version = new NpgsqlCommand("select coalesce(max(version), 0) from hangfire.schema", connection);
		return Convert.ToInt32(await version.ExecuteScalarAsync(cancellationToken)) >= _latestSchemaVersion;
	}

	private static int LatestHangfireSchemaVersion() =>
		typeof(PostgreSqlStorage).Assembly.GetManifestResourceNames()
			.Select(name => Regex.Match(name, @"Install\.v(\d+)\.sql$"))
			.Where(match => match.Success)
			.Max(match => int.Parse(match.Groups[1].Value));
}