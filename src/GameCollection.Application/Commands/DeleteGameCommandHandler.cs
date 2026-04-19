using GameCollection.Application.Abstractions;
using GameCollection.Domain.Repositories;
using GameCollection.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GameCollection.Application.Commands;

public class DeleteGameCommandHandler : IRequestHandler<DeleteGameCommand>
{
	private readonly ICollectedGameRepository _repository;
	private readonly IBggCollectionWriter _bggCollectionWriter;
	private readonly ILogger<DeleteGameCommandHandler> _logger;

	public DeleteGameCommandHandler(
		ICollectedGameRepository repository,
		IBggCollectionWriter bggCollectionWriter,
		ILogger<DeleteGameCommandHandler> logger)
	{
		_repository = repository;
		_bggCollectionWriter = bggCollectionWriter;
		_logger = logger;
	}

	public async Task Handle(DeleteGameCommand request, CancellationToken cancellationToken)
	{
		var id = GameId.From(request.Id);
		var game = await _repository.GetByIdAsync(id, cancellationToken);

		if (game is not null && game.IsBggSourced)
		{
			try
			{
				await _bggCollectionWriter.RemoveFromCollectionAsync(game.BggCollId, cancellationToken);
			}
			catch (Exception ex)
			{
				// BGG write failure is non-fatal — SQLite delete proceeds regardless
				_logger.LogWarning(ex, "Failed to remove collId {CollId} from BGG collection before delete.", game.BggCollId);
			}
		}

		await _repository.DeleteAsync(id, cancellationToken);
	}
}
