using BggIntegration.Application.Services;
using BggIntegration.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BggIntegration.Application.Services;

/// <summary>
/// Probes BGG on startup and then on an adaptive schedule to track runtime availability.
/// Polls every 2 minutes while BGG is unavailable for fast recovery detection, and every 15 minutes while available to minimise unnecessary traffic.
/// On a restore transition, triggers an immediate BGG sync so the collection catches up without waiting for the next scheduled sync interval.
/// </summary>
public sealed class BggHealthCheckBackgroundService : BackgroundService
{
	private static readonly TimeSpan _pollingIntervalWhenDown = TimeSpan.FromMinutes(2);
	private static readonly TimeSpan _pollingIntervalWhenUp = TimeSpan.FromMinutes(15);

	private readonly IBggAvailabilityService _availability;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly BggSettings _settings;
	private readonly ILogger<BggHealthCheckBackgroundService> _logger;

	public BggHealthCheckBackgroundService(
		IBggAvailabilityService availability,
		IServiceScopeFactory scopeFactory,
		IOptions<BggSettings> settings,
		ILogger<BggHealthCheckBackgroundService> logger)
	{
		_availability = availability;
		_scopeFactory = scopeFactory;
		_settings = settings.Value;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Probe immediately on startup so availability state is known before the first request
		await RunProbeAsync(stoppingToken);

		while (!stoppingToken.IsCancellationRequested)
		{
			var delay = _availability.IsAvailable
				? _pollingIntervalWhenUp
				: _pollingIntervalWhenDown;

			try
			{
				await Task.Delay(delay, stoppingToken);
			}
			catch (OperationCanceledException)
			{
				// Application is shutting down
				return;
			}

			await RunProbeAsync(stoppingToken);
		}
	}

	private async Task RunProbeAsync(CancellationToken cancellationToken)
	{
		try
		{
			var restored = await _availability.ProbeAsync(cancellationToken);

			if (restored)
			{
				_logger.LogInformation("BGG connection restored — triggering immediate sync.");
				await TriggerSyncAsync(cancellationToken);
			}
		}
		catch (OperationCanceledException)
		{
			// Application is shutting down
		}
		catch (Exception ex)
		{
			// Probe failures are non-fatal — the next iteration will retry
			_logger.LogWarning(ex, "BGG availability probe encountered an unexpected error.");
		}
	}

	private async Task TriggerSyncAsync(CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(_settings.Username))
		{
			// No username configured — sync is not enabled
			return;
		}

		try
		{
			await using var scope = _scopeFactory.CreateAsyncScope();
			var syncService = scope.ServiceProvider.GetRequiredService<BggSyncService>();
			await syncService.SyncAsync(_settings.Username, cancellationToken);
		}
		catch (OperationCanceledException)
		{
			// Application is shutting down
		}
		catch (Exception ex)
		{
			// Restore-triggered sync failure is non-fatal — the periodic sync will catch up
			_logger.LogWarning(ex, "BGG restore-triggered sync failed.");
		}
	}
}
