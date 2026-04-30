using BggIntegration.Application.Services;
using BggIntegration.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BggIntegration.Infrastructure;

/// <summary>
/// Single background service that owns both the BGG availability health-check loop
/// and the periodic collection sync loop. Running both within one hosted service
/// avoids the concurrency gap that existed when they were separate services
/// (BggHealthCheckBackgroundService could trigger a sync that raced with
/// BggSyncBackgroundService's own periodic sync).
///
/// The two loops run concurrently via Task.WhenAll. A private _syncRunning guard
/// ensures only one sync executes at a time regardless of which loop triggers it.
/// </summary>
internal sealed class BggBackgroundService : BackgroundService
{
	private static readonly TimeSpan _healthCheckIntervalWhenDown = TimeSpan.FromMinutes(2);
	private static readonly TimeSpan _healthCheckIntervalWhenUp   = TimeSpan.FromMinutes(15);

	private readonly IBggAvailabilityService _availability;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly BggSettings _settings;
	private readonly ILogger<BggBackgroundService> _logger;

	// Interlocked guard — ensures only one sync runs at a time across both loops
	private int _syncRunning = 0;

	public BggBackgroundService(
		IBggAvailabilityService availability,
		IServiceScopeFactory scopeFactory,
		IOptions<BggSettings> settings,
		ILogger<BggBackgroundService> logger)
	{
		_availability = availability;
		_scopeFactory = scopeFactory;
		_settings     = settings.Value;
		_logger       = logger;
	}

	protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
		Task.WhenAll(
			RunHealthCheckLoopAsync(stoppingToken),
			RunSyncLoopAsync(stoppingToken));

	private async Task RunHealthCheckLoopAsync(CancellationToken ct)
	{
		// Probe immediately on startup so availability state is known before the first request
		await RunProbeAsync(ct);

		while (!ct.IsCancellationRequested)
		{
			// Task.Delay rather than PeriodicTimer because the interval is adaptive:
			// 2 min while BGG is down (fast recovery), 15 min while up (reduce traffic).
			// PeriodicTimer does not support changing its interval after construction.
			var delay = _availability.IsAvailable
				? _healthCheckIntervalWhenUp
				: _healthCheckIntervalWhenDown;

			try
			{
				await Task.Delay(delay, ct);
			}
			catch (OperationCanceledException)
			{
				// Application is shutting down
				return;
			}

			await RunProbeAsync(ct);
		}
	}

	private async Task RunProbeAsync(CancellationToken ct)
	{
		try
		{
			var restored = await _availability.ProbeAsync(ct);

			if (restored)
			{
				_logger.LogInformation("BGG connection restored — triggering immediate sync.");
				await RunSyncAsync(ct);
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

	private async Task RunSyncLoopAsync(CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(_settings.Username))
		{
			_logger.LogInformation("BGG sync disabled — no username configured.");
			return;
		}

		// Run sync immediately on startup (after the health-check probe has had a chance to set availability)
		await RunSyncAsync(ct);

		var interval = TimeSpan.FromHours(_settings.SyncIntervalHours > 0 ? _settings.SyncIntervalHours : 6);
		using var timer = new PeriodicTimer(interval);

		while (await timer.WaitForNextTickAsync(ct))
		{
			await RunSyncAsync(ct);
		}
	}

	private async Task RunSyncAsync(CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(_settings.Username))
		{
			_logger.LogInformation("BGG sync skipped — no username configured.");
			return;
		}

		if (!_availability.IsAvailable)
		{
			_logger.LogInformation("BGG sync skipped — BGG is currently unavailable.");
			return;
		}

		if (Interlocked.CompareExchange(ref _syncRunning, 1, 0) != 0)
		{
			_logger.LogInformation("BGG sync skipped — a sync is already in progress.");
			return;
		}

		try
		{
			await using var scope = _scopeFactory.CreateAsyncScope();
			var syncService = scope.ServiceProvider.GetRequiredService<BggSyncService>();
			await syncService.SyncAsync(_settings.Username, ct);
		}
		catch (OperationCanceledException)
		{
			// Application is shutting down — this is expected
		}
		catch (Exception ex)
		{
			// Sync failure is non-fatal — will retry at next interval
			_logger.LogWarning(ex, "BGG sync failed — will retry at next interval.");
		}
		finally
		{
			Interlocked.Exchange(ref _syncRunning, 0);
		}
	}
}
