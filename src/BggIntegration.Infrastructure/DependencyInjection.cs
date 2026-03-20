using BggIntegration.Domain.Interfaces;
using BggIntegration.Infrastructure.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;

namespace BggIntegration.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBggIntegrationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var bearerToken = configuration["Bgg:BearerToken"];

        services.AddHttpClient<IBggClient, BggHttpClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "BoardGameCollectionInsights/1.0");

            if (!string.IsNullOrWhiteSpace(bearerToken))
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", bearerToken.Trim());
        })
        .AddStandardResilienceHandler();

        return services;
    }
}
