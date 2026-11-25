using GameCollection.Infrastructure.Postgres;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Api.Tests;

internal sealed class ApiFactory : WebApplicationFactory<Program>
{
	private readonly NpgsqlConnectionStringBuilder _database;
	private readonly NpgsqlConnectionStringBuilder _jobsDatabase;

	public ApiFactory(string databaseConnectionString, string jobsDatabaseConnectionString)
	{
		PostgresClientSettings.Apply();
		_database = new NpgsqlConnectionStringBuilder(databaseConnectionString);
		_jobsDatabase = new NpgsqlConnectionStringBuilder(jobsDatabaseConnectionString);
		GlobalConfiguration.Configuration.UsePostgreSqlStorage(
			options => options.UseNpgsqlConnection(jobsDatabaseConnectionString),
			new PostgreSqlStorageOptions { PrepareSchemaIfNecessary = true, SchemaName = "hangfire" });
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		Apply(builder, "Database", _database);
		Apply(builder, "JobsDatabase", _jobsDatabase);
		builder.UseSetting("Jobs:HangfireSchemaWaitSeconds", "1");

		builder.ConfigureServices(services => services.RemoveAll<IHostedService>());
	}

	private static void Apply(IWebHostBuilder builder, string section, NpgsqlConnectionStringBuilder connection)
	{
		builder.UseSetting($"{section}:Host", connection.Host);
		builder.UseSetting($"{section}:Port", connection.Port.ToString());
		builder.UseSetting($"{section}:Name", connection.Database);
		builder.UseSetting($"{section}:User", connection.Username);
		builder.UseSetting($"{section}:Password", connection.Password);
	}
}