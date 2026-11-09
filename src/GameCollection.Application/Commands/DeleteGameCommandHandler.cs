using GameCollection.Application.Abstractions;
using GameCollection.Application.DTOs;
using GameCollection.Domain.Repositories;
using GameCollection.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GameCollection.Application.Commands;

public class DeleteGameCommandHandler : IRequestHandler<DeleteGameCommand, DeleteGameResult>
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

	public async Task<DeleteGameResult> Handle(DeleteGameCommand request, CancellationToken cancellationToken)
	{
		var id = GameId.From(request.Id);
		var game = await _repository.GetByIdAsync(id, cancellationToken);

		var outcome = game is not null && game.IsBggSourced
			? await RemoveFromBggAsync(game.BggCollId, cancellationToken)
			: BggWriteOutcome.None;

		await _repository.DeleteAsync(id, cancellationToken);

		return new DeleteGameResult(outcome);
	}

	private async Task<BggWriteOutcome> RemoveFromBggAsync(long? bggCollId, CancellationToken cancellationToken)
	{
		try
		{
			return await _bggCollectionWriter.RemoveFromCollectionAsync(bggCollId, cancellationToken);
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
		{
			// BGG write failure is non-fatal — the local delete proceeds regardless
			_logger.LogWarning(ex, "Failed to remove collId {CollId} from BGG collection before delete.", bggCollId);
			return BggWriteOutcome.Failed;
		}
	}
}