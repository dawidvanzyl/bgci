using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure.Http;
using Jobs.Application;
using Jobs.Application.Abstractions;
using Jobs.Application.LiveTest;
using Jobs.Infrastructure.Jobs;

namespace Jobs.Worker.Bgg;

public sealed class BggCollectionWriterAdapter : IBggCollectionWriter
{
	private static readonly TimeSpan _writeTimeLimit = TimeSpan.FromSeconds(60);

	private readonly IBggAccountClients _accounts;
	private readonly TimeProvider _timeProvider;

	public BggCollectionWriterAdapter(IBggAccountClients accounts, TimeProvider timeProvider)
	{
		_accounts = accounts;
		_timeProvider = timeProvider;
	}

	public async Task<long> AddAsync(string account, int bggId, string name, CancellationToken cancellationToken = default)
	{
		var (client, settings) = _accounts.Get(account);
		RequireLogin(settings);
		RequireBearerToken(settings);
		return await StopAfterRejectedTestLoginAsync(account, BggLiveTestSteps.Add, async () =>
		{
			var collectionItemId = await WithinTimeLimitAsync(
				"add",
				token => client.AddToCollectionAsync(bggId, name, token),
				cancellationToken);

			return collectionItemId > 0
				? collectionItemId
				: throw new BggWriteException("add", $"BGG returned no collection item id for bggId {bggId}.");
		});
	}

	public Task RemoveAsync(string account, long collectionItemId, CancellationToken cancellationToken = default)
	{
		var (client, settings) = _accounts.Get(account);
		RequireLogin(settings);
		return StopAfterRejectedTestLoginAsync(account, BggLiveTestSteps.Remove, () => WithinTimeLimitAsync(
			"remove",
			async token =>
			{
				await client.RemoveFromCollectionAsync(collectionItemId, token);
				return collectionItemId;
			},
			cancellationToken));
	}

	public Task<long?> FindCollectionItemIdAsync(string account, int bggId, CancellationToken cancellationToken = default)
	{
		var (client, settings) = _accounts.Get(account);
		RequireUsername(settings);
		RequireBearerToken(settings);
		return WithinTimeLimitAsync(
			"collection item id lookup",
			token => client.FindCollectionItemIdAsync(bggId, token),
			cancellationToken);
	}

	// Repeated rejected logins risk locking the BGG test account, so a rejected login ends the job at once.
	private static async Task<T> StopAfterRejectedTestLoginAsync<T>(string account, string phase, Func<Task<T>> call)
	{
		try
		{
			return await call();
		}
		catch (BggWriteException ex) when (account == BggAccounts.Test && IsRejectedLogin(ex, phase))
		{
			throw new BggFinalAttemptException(ex.Message, ex);
		}
	}

	private static bool IsRejectedLogin(BggWriteException exception, string phase) =>
		BggLiveTestFailureClassifier.ClassifyJobError(exception.Message, phase) is
		{
			Step: BggLiveTestSteps.Login,
			Kind: BggLiveTestFailureKind.LoginRejected
		};

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

	private static void RequireLogin(BggSettings settings)
	{
		RequireUsername(settings);
		if (string.IsNullOrWhiteSpace(settings.Password))
		{
			throw new BggWriteException("configuration", "The BGG password is not configured.");
		}
	}

	private static void RequireUsername(BggSettings settings)
	{
		if (string.IsNullOrWhiteSpace(settings.Username))
		{
			throw new BggWriteException("configuration", "The BGG username is not configured.");
		}
	}

	private static void RequireBearerToken(BggSettings settings)
	{
		if (string.IsNullOrWhiteSpace(settings.BearerToken))
		{
			throw new BggWriteException("configuration", "The BGG bearer token is not configured.");
		}
	}
}