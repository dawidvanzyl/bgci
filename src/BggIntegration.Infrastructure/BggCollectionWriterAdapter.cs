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

	public async Task<BggAddResult> AddToCollectionAsync(int bggId, CancellationToken cancellationToken = default)
	{
		if (!IsConfigured())
		{
			return new BggAddResult(BggWriteOutcome.Skipped, null);
		}

		try
		{
			var collId = await _bggWriterClient.AddToCollectionAsync(_settings.Username, bggId, cancellationToken);
			if (collId <= 0)
			{
				_logger.LogWarning("BGG writer returned no collection item id for bggId {BggId}.", bggId);
				return new BggAddResult(BggWriteOutcome.Failed, null);
			}

			return new BggAddResult(BggWriteOutcome.Succeeded, collId);
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
		{
			// A BGG failure or writer timeout is an outcome; only a cancelled request propagates
			_logger.LogWarning(ex, "Failed to add bggId {BggId} to BGG collection for user {Username}.", bggId, _settings.Username);
			return new BggAddResult(BggWriteOutcome.Failed, null);
		}
	}

	public async Task<BggWriteOutcome> RemoveFromCollectionAsync(long? bggCollId, CancellationToken cancellationToken = default)
	{
		if (!IsConfigured())
		{
			return BggWriteOutcome.Skipped;
		}

		if (bggCollId is null or <= 0)
		{
			_logger.LogWarning("BGG RemoveFromCollection failed — no collId available.");
			return BggWriteOutcome.Failed;
		}

		try
		{
			await _bggWriterClient.RemoveFromCollectionAsync(_settings.Username, bggCollId.Value, cancellationToken);
			return BggWriteOutcome.Succeeded;
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
		{
			// A BGG failure or writer timeout is an outcome; only a cancelled request propagates
			_logger.LogWarning(ex, "Failed to remove collId {CollId} from BGG collection.", bggCollId);
			return BggWriteOutcome.Failed;
		}
	}

	private bool IsConfigured() =>
		!string.IsNullOrWhiteSpace(_settings.Username) && !string.IsNullOrWhiteSpace(_writerSettings.BaseUrl);
}