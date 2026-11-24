using GameCollection.Domain.Repositories;
using GameCollection.Domain.ValueObjects;
using MediatR;

namespace GameCollection.Application.Commands;

public class MarkGameBggFailedCommandHandler : IRequestHandler<MarkGameBggFailedCommand, bool>
{
	private readonly ICollectedGameRepository _repository;

	public MarkGameBggFailedCommandHandler(ICollectedGameRepository repository)
	{
		_repository = repository;
	}

	public async Task<bool> Handle(MarkGameBggFailedCommand request, CancellationToken cancellationToken)
	{
		var game = await _repository.GetByIdAsync(GameId.From(request.GameId), cancellationToken);
		if (game is null)
			return false;

		game.MarkBggFailed();
		await _repository.UpdateBggStatusAsync(game, cancellationToken);
		return true;
	}
}
