using GameCollection.Domain.Repositories;
using GameCollection.Domain.ValueObjects;
using MediatR;

namespace GameCollection.Application.Commands;

public class UpdateGameCommandHandler : IRequestHandler<UpdateGameCommand>
{
    private readonly ICollectedGameRepository _repository;

    public UpdateGameCommandHandler(ICollectedGameRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(UpdateGameCommand request, CancellationToken cancellationToken)
    {
        var game = await _repository.GetByIdAsync(GameId.From(request.Id), cancellationToken)
            ?? throw new KeyNotFoundException($"Game with ID {request.Id} not found.");

        game.UpdateDetails(
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
            mechanics: request.Mechanics
        );

        await _repository.UpdateAsync(game, cancellationToken);
    }
}
