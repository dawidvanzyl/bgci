using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure.Http;
using GameCollection.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BggIntegration.Infrastructure;

/// <summary>
/// Adapts IBggWriterClient write operations to the IBggCollectionWriter interface,
/// resolving the configured username internally so GameCollection.Application stays
/// free of BGG concerns.
/// </summary>
public class BggCollectionWriterAdapter : IBggCollectionWriter
{
	private readonly IBggWriterClient _bggWriterClient;
	private readonly BggSettings _settings;
	private readonly BggWriterSettings _writerSettings;
	private readonly ILogger<BggCollectionWriterAdapter> _logger;

	public BggCollectionWriterAdapter(
		IBggWriterClient bggWriterClient,
		IOptions<BggSettings> settings,
		IOptions<BggWriterSettings> writerSettings,
		ILogger<BggCollectionWriterAdapter> logger)
	{
		_bggWriterClient = bggWriterClient;
		_settings = settings.Value;
		_writerSettings = writerSettings.Value;
		_logger = logger;
	}

	public async Task<long?> AddToCollectionAsync(int bggId, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_writerSettings.BaseUrl))
		{
			// BGG writer not configured — skip silently
			return null;
		}

		try
		{
			return await _bggWriterClient.AddToCollectionAsync(_settings.Username, bggId, cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to add bggId {BggId} to BGG collection for user {Username}.", bggId, _settings.Username);
			return null;
		}
	}

	public async Task RemoveFromCollectionAsync(long? bggCollId, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(_writerSettings.BaseUrl))
		{
			// BGG writer not configured — skip silently
			return;
		}

		if (bggCollId is null or <= 0)
		{
			_logger.LogWarning("BGG RemoveFromCollection skipped — no collId available.");
			return;
		}

		try
		{
			await _bggWriterClient.RemoveFromCollectionAsync(_settings.Username, bggCollId.Value, cancellationToken);
		}
		catch (Exception ex)
		{
			// BGG write failure is non-fatal — SQLite delete proceeds regardless
			_logger.LogWarning(ex, "Failed to remove collId {CollId} from BGG collection.", bggCollId);
		}
	}
}
