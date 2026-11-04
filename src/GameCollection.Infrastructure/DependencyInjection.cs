using GameCollection.Domain.Repositories;
using GameCollection.Infrastructure.Postgres;
using GameCollection.Infrastructure.Postgres.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GameCollection.Infrastructure;

public static class DependencyInjection
{
	public static IServiceCollection AddGameCollectionInfrastructure(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		var connectionString = BuildConnectionString(configuration);

		services.AddScoped<ICollectedGameRepository>(
			_ => new PostgresCollectedGameRepository(connectionString));

		services.AddSingleton(new DatabaseMigrationRunner(connectionString, MigrationScripts.LoadEmbedded()));

		return services;
	}

	private static string BuildConnectionString(IConfiguration configuration)
	{
		if (!int.TryParse(Require(configuration, "Database:Port"), out var port))
		{
			throw new InvalidOperationException("The configuration key 'Database:Port' must be a number.");
		}

		return new NpgsqlConnectionStringBuilder
		{
			Host = Require(configuration, "Database:Host"),
			Port = port,
			Database = Require(configuration, "Database:Name"),
			Username = Require(configuration, "Database:User"),
			Password = Require(configuration, "Database:Password")
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
