using GameCollection.Domain.Repositories;
using GameCollection.Domain.ValueObjects;
using MediatR;

namespace GameCollection.Application.Commands;

public class MarkGameBggSentCommandHandler : IRequestHandler<MarkGameBggSentCommand, bool>
{
	private readonly ICollectedGameRepository _repository;

	public MarkGameBggSentCommandHandler(ICollectedGameRepository repository)
	{
		_repository = repository;
	}

	public async Task<bool> Handle(MarkGameBggSentCommand request, CancellationToken cancellationToken)
	{
		var game = await _repository.GetByIdAsync(GameId.From(request.GameId), cancellationToken);
		if (game is null)
			return false;

		game.MarkBggSent(request.BggCollId);
		await _repository.UpdateBggStatusAsync(game, cancellationToken);
		return true;
	}
}