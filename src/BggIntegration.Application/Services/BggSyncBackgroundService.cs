using BggIntegration.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BggIntegration.Application.Services;

public class BggSyncBackgroundService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly BggSettings _settings;
	private readonly ILogger<BggSyncBackgroundService> _logger;
	private int _syncRunning = 0;

	public BggSyncBackgroundService(
		IServiceScopeFactory scopeFactory,
		IOptions<BggSettings> settings,
		ILogger<BggSyncBackgroundService> logger)
	{
		_scopeFactory = scopeFactory;
		_settings = settings.Value;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (string.IsNullOrWhiteSpace(_settings.Username))
		{
			_logger.LogInformation("BGG sync disabled — no username configured.");
			return;
		}

		// Run sync immediately on startup
		await RunSyncAsync(stoppingToken);

		var interval = TimeSpan.FromHours(_settings.SyncIntervalHours > 0 ? _settings.SyncIntervalHours : 6);
		using var timer = new PeriodicTimer(interval);

		while (await timer.WaitForNextTickAsync(stoppingToken))
		{
			await RunSyncAsync(stoppingToken);
		}
	}

	private async Task RunSyncAsync(CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(_settings.Username))
		{
			_logger.LogInformation("BGG sync skipped — no username configured.");
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
			await syncService.SyncAsync(_settings.Username, cancellationToken);
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
