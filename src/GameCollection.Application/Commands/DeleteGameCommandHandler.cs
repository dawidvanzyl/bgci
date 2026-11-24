using GameCollection.Application.Abstractions;
using GameCollection.Domain.Repositories;
using GameCollection.Domain.ValueObjects;
using MediatR;

namespace GameCollection.Application.Commands;

public class DeleteGameCommandHandler : IRequestHandler<DeleteGameCommand>
{
	private readonly ICollectedGameRepository _repository;
	private readonly IBggWriteQueue _bggWriteQueue;

	public DeleteGameCommandHandler(ICollectedGameRepository repository, IBggWriteQueue bggWriteQueue)
	{
		_repository = repository;
		_bggWriteQueue = bggWriteQueue;
	}

	public async Task Handle(DeleteGameCommand request, CancellationToken cancellationToken)
	{
		var id = GameId.From(request.Id);
		var game = await _repository.GetByIdAsync(id, cancellationToken)
			?? throw new KeyNotFoundException($"Game {request.Id} was not found.");

		await _repository.DeleteAsync(id, cancellationToken);

		if (game.IsBggSourced)
			await _bggWriteQueue.QueueRemoveAsync(game.Id.Value, game.BggId!.Value, game.BggCollId, cancellationToken);
	}
}
