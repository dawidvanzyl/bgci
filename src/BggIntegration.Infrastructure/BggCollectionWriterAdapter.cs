using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure.Http;
using GameCollection.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BggIntegration.Infrastructure;

/// <summary>
/// Adapts IBggWriteClient write operations to the IBggCollectionWriter interface,
/// resolving the configured credentials internally so GameCollection.Application stays
/// free of BGG concerns.
/// </summary>
public class BggCollectionWriterAdapter : IBggCollectionWriter
{
	private static readonly TimeSpan _writeTimeLimit = TimeSpan.FromSeconds(60);

	private readonly IBggWriteClient _bggWriteClient;
	private readonly BggSettings _settings;
	private readonly TimeProvider _timeProvider;
	private readonly ILogger<BggCollectionWriterAdapter> _logger;

	public BggCollectionWriterAdapter(
		IBggWriteClient bggWriteClient,
		IOptions<BggSettings> settings,
		TimeProvider timeProvider,
		ILogger<BggCollectionWriterAdapter> logger)
	{
		_bggWriteClient = bggWriteClient;
		_settings = settings.Value;
		_timeProvider = timeProvider;
		_logger = logger;
	}

	public async Task<BggAddResult> AddToCollectionAsync(int bggId, string name, CancellationToken cancellationToken = default)
	{
		if (!IsConfigured())
		{
			return new BggAddResult(BggWriteOutcome.Skipped, null);
		}

		try
		{
			using var timeLimit = new CancellationTokenSource(_writeTimeLimit, _timeProvider);
			using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeLimit.Token);
			var collId = await _bggWriteClient.AddToCollectionAsync(bggId, name, linked.Token);
			if (collId <= 0)
			{
				_logger.LogWarning("BGG returned no collection item id for bggId {BggId}.", bggId);
				return new BggAddResult(BggWriteOutcome.Failed, null);
			}

			return new BggAddResult(BggWriteOutcome.Succeeded, collId);
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
		{
			// A BGG failure or the time limit is an outcome; only a request the caller cancelled propagates
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
			using var timeLimit = new CancellationTokenSource(_writeTimeLimit, _timeProvider);
			using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeLimit.Token);
			await _bggWriteClient.RemoveFromCollectionAsync(bggCollId.Value, linked.Token);
			return BggWriteOutcome.Succeeded;
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
		{
			// A BGG failure or the time limit is an outcome; only a request the caller cancelled propagates
			_logger.LogWarning(ex, "Failed to remove collId {CollId} from BGG collection.", bggCollId);
			return BggWriteOutcome.Failed;
		}
	}

	private bool IsConfigured() =>
		!string.IsNullOrWhiteSpace(_settings.Username) && !string.IsNullOrWhiteSpace(_settings.Password);
}