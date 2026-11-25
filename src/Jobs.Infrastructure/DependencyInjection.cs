using GameCollection.Application.Abstractions;
using Hangfire;
using Hangfire.PostgreSql;
using Jobs.Application.Abstractions;
using Jobs.Infrastructure.Postgres;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Shared.Persistence.Migrations;

namespace Jobs.Infrastructure;

public static class DependencyInjection
{
	public static IServiceCollection AddJobsInfrastructure(
		this IServiceCollection services,
		IConfiguration configuration,
		bool installHangfireSchema)
	{
		var connectionString = BuildConnectionString(configuration);
		var queuePollInterval = ReadSeconds(configuration, "Jobs:QueuePollIntervalSeconds");

		services.AddHangfire(config => config
			.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
			.UseSimpleAssemblyNameTypeSerializer()
			.UseRecommendedSerializerSettings()
			.UsePostgreSqlStorage(
				options => options.UseNpgsqlConnection(connectionString),
				CreateStorageOptions(queuePollInterval, installHangfireSchema))
			.UseFilter(new JobHistoryRetentionFilter()));

		services.TryAddSingleton(TimeProvider.System);

		services.AddScoped(_ => new PostgresBggWriteRequestRepository(connectionString));
		services.AddScoped<IBggWriteRequestRepository>(provider => provider.GetRequiredService<PostgresBggWriteRequestRepository>());
		services.AddScoped<IBggLiveTestLog>(provider => provider.GetRequiredService<PostgresBggWriteRequestRepository>());
		services.AddScoped<IBggTestWriteQueue, HangfireBggTestWriteQueue>();
		services.AddScoped<IBggWriteRequestReset>(provider => provider.GetRequiredService<PostgresBggWriteRequestRepository>());
		services.AddScoped<IBggWriteQueue, HangfireBggWriteQueue>();

		services.AddSingleton(new JobsDatabaseMigrator(
			connectionString,
			new DatabaseMigrationRunner(
				connectionString,
				MigrationScripts.LoadEmbedded(typeof(DependencyInjection).Assembly, "Jobs.Infrastructure.Postgres.Scripts."))));

		return services;
	}

	private static PostgreSqlStorageOptions CreateStorageOptions(TimeSpan? queuePollInterval, bool installHangfireSchema)
	{
		var options = new PostgreSqlStorageOptions { PrepareSchemaIfNecessary = installHangfireSchema, SchemaName = "hangfire" };
		if (queuePollInterval is { } interval)
		{
			options.QueuePollInterval = interval;
		}

		return options;
	}

	private static TimeSpan? ReadSeconds(IConfiguration configuration, string key)
	{
		var value = configuration[key];
		return string.IsNullOrWhiteSpace(value)
			? null
			: double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var seconds) && seconds > 0
			? TimeSpan.FromSeconds(seconds)
			: throw new InvalidOperationException($"The configuration key '{key}' must be a positive number of seconds.");
	}

	private static string BuildConnectionString(IConfiguration configuration)
	{
		var port = int.TryParse(Require(configuration, "JobsDatabase:Port"), out var parsedPort)
			? parsedPort
			: throw new InvalidOperationException("The configuration key 'JobsDatabase:Port' must be a number.");

		return new NpgsqlConnectionStringBuilder
		{
			Host = Require(configuration, "JobsDatabase:Host"),
			Port = port,
			Database = Require(configuration, "JobsDatabase:Name"),
			Username = Require(configuration, "JobsDatabase:User"),
			Password = Require(configuration, "JobsDatabase:Password")
		}.ConnectionString;
	}

	private static string Require(IConfiguration configuration, string key)
	{
		var value = configuration[key];
		return string.IsNullOrWhiteSpace(value)
			? throw new InvalidOperationException($"The configuration key '{key}' is missing or empty.")
			: value;
	}
}