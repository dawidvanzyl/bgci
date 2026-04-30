using BggIntegration.Application.Services;
using BggIntegration.Infrastructure.Constants;
using Microsoft.Extensions.Logging;

namespace BggIntegration.Infrastructure;

/// <summary>
/// Probes BGG with a lightweight HTTP request to determine runtime reachability.
/// Uses a dedicated named HttpClient with no retry pipeline so failures are detected
/// immediately rather than after multiple retry attempts with backoff.
/// Registered as a singleton — state is intentionally in-memory only and resets on restart.
/// </summary>
public sealed class BggAvailabilityService : IBggAvailabilityService
{
	internal const string _httpClientName = "BggHealthCheck";

	private readonly IHttpClientFactory _httpClientFactory;
	private readonly ILogger<BggAvailabilityService> _logger;

	// volatile ensures cross-thread visibility without locking for simple bool reads
	private volatile bool _isAvailable = false;
	private readonly object _stateLock = new();

	public bool IsAvailable => _isAvailable;
	public DateTime? LastChecked { get; private set; } = null;

	public BggAvailabilityService(IHttpClientFactory httpClientFactory, ILogger<BggAvailabilityService> logger)
	{
		_httpClientFactory = httpClientFactory;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<bool> ProbeAsync(CancellationToken cancellationToken)
	{
		var wasAvailable = _isAvailable;

		bool probeSucceeded;
		try
		{
			using var http = _httpClientFactory.CreateClient(_httpClientName);

			// A lightweight search query — small response, no authentication required
			using var response = await http.GetAsync(
				string.Format(BggApiEndpoints.Search, "Catan"),
				HttpCompletionOption.ResponseHeadersRead,
				cancellationToken);

			probeSucceeded = response.IsSuccessStatusCode;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Host is shutting down — propagate so the caller can clean up
			throw;
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
		{
			// Network-level failure or HttpClient timeout — BGG is unreachable
			probeSucceeded = false;
		}

		lock (_stateLock)
		{
			_isAvailable = probeSucceeded;
			LastChecked = DateTime.UtcNow;
		}

		if (!wasAvailable && probeSucceeded)
		{
			_logger.LogInformation("BGG connection restored.");
			return true; // signals a restore occurred
		}

		if (wasAvailable && !probeSucceeded)
		{
			_logger.LogWarning("BGG became unavailable.");
		}

		return false;
	}
}
