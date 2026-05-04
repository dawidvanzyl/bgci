using BggIntegration.Domain.Interfaces;
using GameCollection.Application.Commands;
using GameCollection.Application.Queries;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BggIntegration.Application.Services;

public class BggSyncService
{
	private static int _syncRunning = 0;

	private readonly IBggClient _bggClient;
	private readonly IMediator _mediator;
	private readonly ILogger<BggSyncService> _logger;

	public BggSyncService(IBggClient bggClient, IMediator mediator, ILogger<BggSyncService> logger)
	{
		_bggClient = bggClient;
		_mediator = mediator;
		_logger = logger;
	}

	public async Task SyncAsync(string username, CancellationToken cancellationToken)
	{
		if (Interlocked.CompareExchange(ref _syncRunning, 1, 0) != 0)
		{
			_logger.LogInformation("BGG sync skipped — a sync is already in progress.");
			return;
		}

		try
		{
			await SyncCoreAsync(username, cancellationToken);
		}
		finally
		{
			Interlocked.Exchange(ref _syncRunning, 0);
		}
	}

	private async Task SyncCoreAsync(string username, CancellationToken cancellationToken)
	{
		_logger.LogInformation("BGG sync starting for user {Username}.", username);

		// 1. Fetch BGG base collection and expansion collection
		var bggCollection = await _bggClient.GetCollectionAsync(username, cancellationToken);
		var bggCollectionById = bggCollection.ToDictionary(c => c.BggId);

		var bggExpansionCollection = await _bggClient.GetExpansionCollectionAsync(username, cancellationToken);
		var bggExpansionCollectionById = bggExpansionCollection.ToDictionary(c => c.BggId);

		var allBggIds = bggCollectionById.Keys.Concat(bggExpansionCollectionById.Keys).ToHashSet();

		// 2. Fetch all SQLite games that have a bgg_id
		var allGames = await _mediator.Send(new GetAllGamesQuery(), cancellationToken);
		var localBggGames = allGames
			.Where(g => g.BggId.HasValue)
			.ToList();
		var localBggGamesByBggId = localBggGames.ToDictionary(g => g.BggId!.Value);

		// 3. Compute diffs
		var toAddBase      = bggCollectionById.Keys.Except(localBggGamesByBggId.Keys).ToList();
		var toAddExpansion = bggExpansionCollectionById.Keys.Except(localBggGamesByBggId.Keys).ToList();
		var toDelete       = localBggGamesByBggId.Keys.Except(allBggIds).ToList();

		var added   = 0;
		var deleted = 0;

		// 4. Add base games in BGG but not in SQLite
		foreach (var bggId in toAddBase)
		{
			try
			{
				var details = await _bggClient.GetGameDetailsAsync(bggId, cancellationToken);
				if (details is null)
				{
					_logger.LogWarning("BGG sync: could not fetch details for bggId {BggId} — skipping.", bggId);
					continue;
				}

				var command = new AddGameFromBggCommand(
					Name: details.Name,
					Year: details.Year,
					Description: details.Description,
					MinPlayers: details.MinPlayers,
					MaxPlayers: details.MaxPlayers,
					PlayTimeMinutes: details.PlayTimeMinutes,
					BggRating: details.AverageRating,
					CoverImageUrl: details.ImageUrl ?? details.ThumbnailUrl,
					Categories: details.Categories.ToList(),
					Mechanics: details.Mechanics.ToList(),
					BggId: details.BggId,
					BggCollId: bggCollectionById[bggId].CollId,
					SkipBggWrite: true
				);

				await _mediator.Send(command, cancellationToken);
				added++;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "BGG sync: failed to add bggId {BggId} — skipping.", bggId);
			}
		}

		// 5. Add expansions in BGG but not in SQLite
		if (toAddExpansion.Count > 0)
		{
			// Refresh local games to pick up any base games just added in this sync run
			var refreshed = await _mediator.Send(new GetAllGamesQuery(), cancellationToken);
			var refreshedByBggId = refreshed
				.Where(g => g.BggId.HasValue)
				.ToDictionary(g => g.BggId!.Value);

			foreach (var bggId in toAddExpansion)
			{
				try
				{
					var details = await _bggClient.GetGameDetailsAsync(bggId, cancellationToken);
					if (details is null)
					{
						_logger.LogWarning("BGG sync: could not fetch expansion details for bggId {BggId} — skipping.", bggId);
						continue;
					}

				// Resolve parent: first matching parent bggId that exists locally
				Guid? parentGameId = null;
				foreach (var parentBggId in details.ParentBggIds)
				{
					if (refreshedByBggId.TryGetValue(parentBggId, out var parentGame))
					{
						parentGameId = parentGame.Id;
						break;
					}
				}

				if (parentGameId is null)
				{
					_logger.LogWarning("BGG sync: expansion bggId {BggId} has no matching parent game in local collection — skipping.", bggId);
					continue;
				}

				var command = new AddGameFromBggCommand(
						Name: details.Name,
						Year: details.Year,
						Description: details.Description,
						MinPlayers: details.MinPlayers,
						MaxPlayers: details.MaxPlayers,
						PlayTimeMinutes: details.PlayTimeMinutes,
						BggRating: details.AverageRating,
						CoverImageUrl: details.ImageUrl ?? details.ThumbnailUrl,
						Categories: details.Categories.ToList(),
						Mechanics: details.Mechanics.ToList(),
						BggId: details.BggId,
						BggCollId: bggExpansionCollectionById[bggId].CollId,
						SkipBggWrite: true,
						ParentGameId: parentGameId
					);

					await _mediator.Send(command, cancellationToken);
					added++;
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "BGG sync: failed to add expansion bggId {BggId} — skipping.", bggId);
				}
			}
		}

		// 6. Delete games in SQLite that are no longer in BGG collection (base or expansion)
		foreach (var bggId in toDelete)
		{
			try
			{
				var localGame = localBggGamesByBggId[bggId];
				await _mediator.Send(new DeleteGameCommand(localGame.Id), cancellationToken);
				deleted++;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "BGG sync: failed to delete game with bggId {BggId} — skipping.", bggId);
			}
		}

		_logger.LogInformation("BGG sync complete for user {Username} — added {Added}, deleted {Deleted}.", username, added, deleted);
	}
}
