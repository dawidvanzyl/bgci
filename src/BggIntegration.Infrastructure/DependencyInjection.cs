using BggIntegration.Application.Translation;
using BggIntegration.Domain.Interfaces;
using BggIntegration.Infrastructure.Http;
using Microsoft.Extensions.DependencyInjection;

namespace BggIntegration.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBggIntegrationInfrastructure(this IServiceCollection services)
    {
        services.AddHttpClient<IBggClient, BggHttpClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "BoardGameCollectionInsights/1.0");
        });

        services.AddScoped<IBggTranslator, BggTranslator>();

        return services;
    }
}
