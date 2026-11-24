using GameCollection.Application.Abstractions;
using GameCollection.Application.DTOs;
using GameCollection.Domain.Aggregates;
using GameCollection.Domain.Repositories;
using GameCollection.Domain.ValueObjects;
using MediatR;

namespace GameCollection.Application.Commands;

public class AddGameFromBggCommandHandler : IRequestHandler<AddGameFromBggCommand, AddGameFromBggResult>
{
	private readonly ICollectedGameRepository _repository;
	private readonly IBggWriteQueue _bggWriteQueue;

	public AddGameFromBggCommandHandler(ICollectedGameRepository repository, IBggWriteQueue bggWriteQueue)
	{
		_repository = repository;
		_bggWriteQueue = bggWriteQueue;
	}

	public async Task<AddGameFromBggResult> Handle(AddGameFromBggCommand request, CancellationToken cancellationToken)
	{
		if (!request.SkipBggWrite && await _bggWriteQueue.HasUnresolvedRemoveAsync(request.BggId, cancellationToken))
			throw new BggRemoveNotFinishedException(request.BggId);

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
			bggCollId: request.SkipBggWrite ? request.BggCollId : null,
			parentGameId: request.ParentGameId.HasValue
				? GameId.From(request.ParentGameId.Value)
				: null,
			bggStatus: request.SkipBggWrite ? BggStatus.Sent : BggStatus.Pending
		);

		await _repository.AddAsync(game, cancellationToken);

		if (!request.SkipBggWrite)
			await QueueAddAsync(game, request, cancellationToken);

		return new AddGameFromBggResult(game.Id.Value, game.BggStatus!.Value.ToText());
	}

	private async Task QueueAddAsync(CollectedGame game, AddGameFromBggCommand request, CancellationToken cancellationToken)
	{
		try
		{
			await _bggWriteQueue.QueueAddAsync(game.Id.Value, request.BggId, request.Name, cancellationToken);
		}
		catch
		{
			await _repository.DeleteAsync(game.Id, CancellationToken.None);
			throw;
		}
	}
}