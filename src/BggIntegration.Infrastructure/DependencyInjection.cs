using BggIntegration.Application.Services;
using BggIntegration.Domain.Interfaces;
using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure.Constants;
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
			client.BaseAddress = new Uri(BggApiConstants.BaseAddress);
			client.Timeout = TimeSpan.FromSeconds(30);
			client.DefaultRequestHeaders.Add("User-Agent", BggApiConstants.UserAgent);

			if (!string.IsNullOrWhiteSpace(bearerToken))
			{
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

		// Dedicated lightweight named HttpClient for availability probing — no Polly retry pipeline
		// so failures are detected immediately rather than after multiple backoff attempts.
		services.AddHttpClient(BggAvailabilityService._httpClientName, client =>
		{
			client.BaseAddress = new Uri(BggApiConstants.BaseAddress);
			client.Timeout = TimeSpan.FromSeconds(10);
			client.DefaultRequestHeaders.Add("User-Agent", BggApiConstants.UserAgent);

			if (!string.IsNullOrWhiteSpace(bearerToken))
			{
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken.Trim());
			}
		});

		// Singleton — availability state is in-memory, intentionally resets on app restart
		services.AddSingleton<IBggAvailabilityService, BggAvailabilityService>();

		services.AddScoped<IBggCollectionWriter, BggCollectionWriterAdapter>();
		services.AddScoped<BggSyncService>();
		services.AddHostedService<BggHealthCheckBackgroundService>();

		return services;
	}
}
