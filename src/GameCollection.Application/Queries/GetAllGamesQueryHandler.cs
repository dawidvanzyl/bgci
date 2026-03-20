using GameCollection.Application.DTOs;
using GameCollection.Domain.Repositories;
using MediatR;

namespace GameCollection.Application.Queries;

public class GetAllGamesQueryHandler : IRequestHandler<GetAllGamesQuery, IReadOnlyList<CollectedGameDto>>
{
    private readonly ICollectedGameRepository _repository;

    public GetAllGamesQueryHandler(ICollectedGameRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<CollectedGameDto>> Handle(GetAllGamesQuery request, CancellationToken cancellationToken)
    {
        var games = await _repository.GetAllAsync(cancellationToken);
        return games
			.Select(g => g.ToDto())
			.ToList()
			.AsReadOnly();
    }
}
