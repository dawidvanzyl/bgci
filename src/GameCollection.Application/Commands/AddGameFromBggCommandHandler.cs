using GameCollection.Application.Abstractions;
using GameCollection.Application.DTOs;
using GameCollection.Domain.Aggregates;
using GameCollection.Domain.Repositories;
using GameCollection.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GameCollection.Application.Commands;

public class AddGameFromBggCommandHandler : IRequestHandler<AddGameFromBggCommand, AddGameFromBggResult>
{
	private readonly ICollectedGameRepository _repository;
	private readonly IBggCollectionWriter _bggCollectionWriter;
	private readonly ILogger<AddGameFromBggCommandHandler> _logger;

	public AddGameFromBggCommandHandler(
		ICollectedGameRepository repository,
		IBggCollectionWriter bggCollectionWriter,
		ILogger<AddGameFromBggCommandHandler> logger)
	{
		_repository = repository;
		_bggCollectionWriter = bggCollectionWriter;
		_logger = logger;
	}

	public async Task<AddGameFromBggResult> Handle(AddGameFromBggCommand request, CancellationToken cancellationToken)
	{
		var bggAdd = request.SkipBggWrite
			? new BggAddResult(BggWriteOutcome.None, request.BggCollId)
			: await AddToBggAsync(request.BggId, cancellationToken);

		var game = CollectedGame.Create(
			name: new GameName(request.Name),
			year: request.Year,
			description: request.Description,
			playerCount: request.MinPlayers.HasValue && request.MaxPlayers.HasValue
				? new PlayerCount(request.MinPlayers.Value, request.MaxPlayers.Value)
				: null,
			playTime: request.PlayTimeMinutes.HasValue
				? new PlayTime(request.PlayTimeMinutes.Value)
				: null,
			minPlayTimeMinutes: request.MinPlayTimeMinutes,
			maxPlayTimeMinutes: request.MaxPlayTimeMinutes,
			bggRating: request.BggRating.HasValue
				? new BggRating(request.BggRating.Value)
				: null,
			bggWeight: request.BggWeight.HasValue
				? new BggWeight(request.BggWeight.Value)
				: null,
			minAge: request.MinAge,
			bestPlayerCountMin: request.BestPlayerCountMin,
			bestPlayerCountMax: request.BestPlayerCountMax,
			coverImageUrl: request.CoverImageUrl is not null
				? new Uri(request.CoverImageUrl)
				: null,
			categories: request.Categories,
			mechanics: request.Mechanics,
			designers: request.Designers,
			artists: request.Artists,
			publishers: request.Publishers,
			subdomains: request.Subdomains,
			bggId: BggGameId.From(request.BggId),
			bggCollId: bggAdd.BggCollId,
			parentGameId: request.ParentGameId.HasValue
				? GameId.From(request.ParentGameId.Value)
				: null
		);

		await _repository.AddAsync(game, cancellationToken);

		return new AddGameFromBggResult(game.Id.Value, bggAdd.Outcome);
	}

	private async Task<BggAddResult> AddToBggAsync(int bggId, CancellationToken cancellationToken)
	{
		try
		{
			return await _bggCollectionWriter.AddToCollectionAsync(bggId, cancellationToken);
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
		{
			// The local add must complete when BGG fails or times out
			_logger.LogWarning(ex, "Failed to add bggId {BggId} to BGG collection.", bggId);
			return new BggAddResult(BggWriteOutcome.Failed, null);
		}
	}
}