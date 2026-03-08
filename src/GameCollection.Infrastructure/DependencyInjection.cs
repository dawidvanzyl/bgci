using GameCollection.Domain.Repositories;
using GameCollection.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace GameCollection.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGameCollectionInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddScoped<ICollectedGameRepository>(
            _ => new SqliteCollectedGameRepository(connectionString));

        services.AddSingleton(new DatabaseMigrator(connectionString));

        return services;
    }
}
