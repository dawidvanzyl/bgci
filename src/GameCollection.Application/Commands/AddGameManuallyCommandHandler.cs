using GameCollection.Domain.Aggregates;
using GameCollection.Domain.Repositories;
using GameCollection.Domain.ValueObjects;
using MediatR;

namespace GameCollection.Application.Commands;

public class AddGameManuallyCommandHandler : IRequestHandler<AddGameManuallyCommand, Guid>
{
    private readonly ICollectedGameRepository _repository;

    public AddGameManuallyCommandHandler(ICollectedGameRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(AddGameManuallyCommand request, CancellationToken cancellationToken)
    {
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
            bggRating: request.BggRating.HasValue
                ? new BggRating(request.BggRating.Value)
                : null,
            coverImageUrl: request.CoverImageUrl is not null
                ? new Uri(request.CoverImageUrl)
                : null,
            categories: request.Categories,
            mechanics: request.Mechanics,
            parentGameId: request.ParentGameId.HasValue
                ? GameId.From(request.ParentGameId.Value)
                : null
        );

        await _repository.AddAsync(game, cancellationToken);
        return game.Id.Value;
    }
}
