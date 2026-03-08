using GameCollection.Domain.Repositories;
using GameCollection.Domain.ValueObjects;
using MediatR;

namespace GameCollection.Application.Commands;

public class DeleteGameCommandHandler : IRequestHandler<DeleteGameCommand>
{
    private readonly ICollectedGameRepository _repository;

    public DeleteGameCommandHandler(ICollectedGameRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(DeleteGameCommand request, CancellationToken cancellationToken)
    {
        await _repository.DeleteAsync(GameId.From(request.Id), cancellationToken);
    }
}
