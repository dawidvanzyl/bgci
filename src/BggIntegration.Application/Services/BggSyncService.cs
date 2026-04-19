using BggIntegration.Domain.Interfaces;
using GameCollection.Application.Commands;
using GameCollection.Application.Queries;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BggIntegration.Application.Services;

public class BggSyncService
{
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
		_logger.LogInformation("BGG sync starting for user {Username}.", username);

		// 1. Fetch BGG collection
		var bggCollection = await _bggClient.GetCollectionAsync(username, cancellationToken);
		var bggCollectionById = bggCollection.ToDictionary(c => c.BggId);

		// 2. Fetch all SQLite games that have a bgg_id
		var allGames = await _mediator.Send(new GetAllGamesQuery(), cancellationToken);
		var localBggGames = allGames
			.Where(g => g.BggId.HasValue)
			.ToList();
		var localBggGamesByBggId = localBggGames.ToDictionary(g => g.BggId!.Value);

		// 3. Compute diff
		var toAdd = bggCollectionById.Keys.Except(localBggGamesByBggId.Keys).ToList();
		var toDelete = localBggGamesByBggId.Keys.Except(bggCollectionById.Keys).ToList();

		var added = 0;
		var deleted = 0;

		// 4. Add games in BGG but not in SQLite
		foreach (var bggId in toAdd)
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

		// 5. Delete games in SQLite that are no longer in BGG collection
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
