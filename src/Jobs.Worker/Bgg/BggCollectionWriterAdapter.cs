using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure.Http;
using Jobs.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Jobs.Worker.Bgg;

public sealed class BggCollectionWriterAdapter : IBggCollectionWriter
{
	private static readonly TimeSpan _writeTimeLimit = TimeSpan.FromSeconds(60);

	private readonly IBggWriteClient _bggWriteClient;
	private readonly BggSettings _settings;
	private readonly TimeProvider _timeProvider;

	public BggCollectionWriterAdapter(
		IBggWriteClient bggWriteClient,
		IOptions<BggSettings> settings,
		TimeProvider timeProvider)
	{
		_bggWriteClient = bggWriteClient;
		_settings = settings.Value;
		_timeProvider = timeProvider;
	}

	public async Task<long> AddAsync(int bggId, string name, CancellationToken cancellationToken = default)
	{
		RequireLogin();
		RequireBearerToken();
		var collectionItemId = await WithinTimeLimitAsync(
			"add",
			token => _bggWriteClient.AddToCollectionAsync(bggId, name, token),
			cancellationToken);

		return collectionItemId > 0
			? collectionItemId
			: throw new BggWriteException("add", $"BGG returned no collection item id for bggId {bggId}.");
	}

	public Task RemoveAsync(long collectionItemId, CancellationToken cancellationToken = default)
	{
		RequireLogin();
		return WithinTimeLimitAsync(
			"remove",
			async token =>
			{
				await _bggWriteClient.RemoveFromCollectionAsync(collectionItemId, token);
				return collectionItemId;
			},
			cancellationToken);
	}

	public Task<long?> FindCollectionItemIdAsync(int bggId, CancellationToken cancellationToken = default)
	{
		RequireUsername();
		RequireBearerToken();
		return WithinTimeLimitAsync(
			"collection item id lookup",
			token => _bggWriteClient.FindCollectionItemIdAsync(bggId, token),
			cancellationToken);
	}

	private async Task<T> WithinTimeLimitAsync<T>(string step, Func<CancellationToken, Task<T>> call, CancellationToken cancellationToken)
	{
		using var timeLimit = new CancellationTokenSource(_writeTimeLimit, _timeProvider);
		using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeLimit.Token);
		try
		{
			return await call(linked.Token);
		}
		catch (OperationCanceledException) when (timeLimit.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			throw new BggWriteException(step, $"BGG {step} exceeded the {_writeTimeLimit.TotalSeconds:0} second time limit.");
		}
	}

	private void RequireLogin()
	{
		RequireUsername();
		if (string.IsNullOrWhiteSpace(_settings.Password))
		{
			throw new BggWriteException("configuration", "The BGG password is not configured.");
		}
	}

	private void RequireUsername()
	{
		if (string.IsNullOrWhiteSpace(_settings.Username))
		{
			throw new BggWriteException("configuration", "The BGG username is not configured.");
		}
	}

	private void RequireBearerToken()
	{
		if (string.IsNullOrWhiteSpace(_settings.BearerToken))
		{
			throw new BggWriteException("configuration", "The BGG bearer token is not configured.");
		}
	}
}
