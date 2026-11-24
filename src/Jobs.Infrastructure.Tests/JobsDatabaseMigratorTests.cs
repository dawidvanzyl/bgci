using GameCollection.Testing.Postgres;
using Hangfire;
using Hangfire.PostgreSql;
using Jobs.Infrastructure.Postgres;
using Shared.Persistence.Migrations;
using Shouldly;
using Xunit;

namespace Jobs.Infrastructure.Tests;

public sealed class JobsDatabaseMigratorTests
{
	private readonly PostgresContainerFixture _postgres;

	public JobsDatabaseMigratorTests(PostgresContainerFixture postgres)
	{
		_postgres = postgres;
	}

	[Fact]
	[Trait("AC", "111UC1")]
	public async Task WaitForHangfireSchemaAsync_NoJobsServiceInstallsIt_ReturnsFalseAfterTheLimit()
	{
		var connectionString = await _postgres.CreateDatabaseAsync();

		var found = await Migrator(connectionString).WaitForHangfireSchemaAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

		found.ShouldBeFalse();
	}

	[Fact]
	[Trait("AC", "111UC1")]
	public async Task WaitForHangfireSchemaAsync_JobsServiceInstallsItLater_ReturnsTrue()
	{
		var connectionString = await _postgres.CreateDatabaseAsync();
		var wait = Migrator(connectionString).WaitForHangfireSchemaAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

		await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
		GlobalConfiguration.Configuration.UsePostgreSqlStorage(
			options => options.UseNpgsqlConnection(connectionString),
			new PostgreSqlStorageOptions { PrepareSchemaIfNecessary = true, SchemaName = "hangfire" });

		(await wait).ShouldBeTrue();
	}

	private static JobsDatabaseMigrator Migrator(string connectionString) =>
		new(connectionString, new DatabaseMigrationRunner(connectionString, new MigrationScripts([], [], [])));
}