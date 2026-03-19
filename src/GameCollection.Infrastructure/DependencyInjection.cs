using GameCollection.Domain.Repositories;
using GameCollection.Infrastructure.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameCollection.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGameCollectionInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
		var dbPath = configuration["Database:Path"] ?? "/data/bgci.db";
		var connectionString = $"Data Source={dbPath}";

		services.AddScoped<ICollectedGameRepository>(
            _ => new SqliteCollectedGameRepository(connectionString));

        services.AddSingleton(new DatabaseMigrator(connectionString));

        return services;
    }
}
