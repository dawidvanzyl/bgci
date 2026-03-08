using GameCollection.Application.DTOs;
using GameCollection.Domain.Repositories;
using GameCollection.Domain.ValueObjects;
using MediatR;

namespace GameCollection.Application.Queries;

public class GetGameByIdQueryHandler : IRequestHandler<GetGameByIdQuery, CollectedGameDto?>
{
    private readonly ICollectedGameRepository _repository;

    public GetGameByIdQueryHandler(ICollectedGameRepository repository)
    {
        _repository = repository;
    }

    public async Task<CollectedGameDto?> Handle(GetGameByIdQuery request, CancellationToken cancellationToken)
    {
        var game = await _repository.GetByIdAsync(GameId.From(request.Id), cancellationToken);
        return game?.ToDto();
    }
}
