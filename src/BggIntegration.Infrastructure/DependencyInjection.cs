using BggIntegration.Application.Services;
using BggIntegration.Domain.Interfaces;
using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure.Constants;
using BggIntegration.Infrastructure.Http;
using GameCollection.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http.Headers;

namespace BggIntegration.Infrastructure;

public static class DependencyInjection
{
	public static IServiceCollection AddBggIntegrationInfrastructure(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		services.Configure<BggSettings>(configuration.GetSection("Bgg"));

		var bearerToken = configuration["Bgg:BearerToken"];
		var bggBaseAddress = ResolveBggBaseAddress(configuration["Bgg:BaseUrl"]);

		services.AddHttpClient<IBggClient, BggHttpClient>(client =>
		{
			client.BaseAddress = bggBaseAddress;
			client.Timeout = TimeSpan.FromSeconds(30);
			client.DefaultRequestHeaders.Add("User-Agent", BggApiConstants.UserAgent);

			if (!string.IsNullOrWhiteSpace(bearerToken))
			{
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken.Trim());
			}
		})
		.AddStandardResilienceHandler();

		services.AddHttpClient<IBggWriteClient, BggWriteHttpClient>(client =>
		{
			client.BaseAddress = bggBaseAddress;
			client.Timeout = TimeSpan.FromSeconds(30);
		})
		.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
		{
			UseCookies = false,
			AllowAutoRedirect = false,
		});

		services.AddSingleton<BggSession>();
		services.TryAddSingleton(TimeProvider.System);

		// Dedicated lightweight named HttpClient for availability probing — no Polly retry pipeline
		// so failures are detected immediately rather than after multiple backoff attempts.
		services.AddHttpClient(BggAvailabilityService._httpClientName, client =>
		{
			client.BaseAddress = bggBaseAddress;
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

		// Hosted service — owns the health-check and sync loops
		services.AddHostedService<BggBackgroundService>();

		return services;
	}

	// The QA stack points Bgg:BaseUrl at a mock server. Production leaves it unset.
	private static Uri ResolveBggBaseAddress(string? configured)
	{
		return string.IsNullOrWhiteSpace(configured)
			? new Uri(BggApiConstants.BaseAddress)
			: new Uri(configured.Trim().TrimEnd('/') + "/");
	}
}