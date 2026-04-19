using BggIntegration.Application.Services;
using BggIntegration.Domain.Interfaces;
using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure.Http;
using GameCollection.Application.Abstractions;
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
		services.Configure<BggSettings>(configuration.GetSection("Bgg"));
		services.Configure<BggWriterSettings>(configuration.GetSection("BggWriter"));

		var bearerToken = configuration["Bgg:BearerToken"];

		services.AddHttpClient<IBggClient, BggHttpClient>(client =>
		{
			client.Timeout = TimeSpan.FromSeconds(30);
			client.DefaultRequestHeaders.Add("User-Agent", "BoardGameCollectionInsights/1.0");

			if (!string.IsNullOrWhiteSpace(bearerToken))
			{
				client.BaseAddress = new Uri("https://boardgamegeek.com/xmlapi2");
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken.Trim());
			}
		})
		.AddStandardResilienceHandler();

		var bggWriterBaseUrl = configuration["BggWriter:BaseUrl"];

		services.AddHttpClient<IBggWriterClient, BggWriterHttpClient>(client =>
		{
			client.Timeout = TimeSpan.FromSeconds(60);

			if (!string.IsNullOrWhiteSpace(bggWriterBaseUrl))
			{
				client.BaseAddress = new Uri(bggWriterBaseUrl.TrimEnd('/'));
			}
		});

		services.AddScoped<IBggCollectionWriter, BggCollectionWriterAdapter>();
		services.AddScoped<BggSyncService>();

		return services;
	}
}
