using MediatR;

namespace GameCollection.Application.Commands;

public record AddGameFromBggCommand(
    string Name,
    int? Year,
    string? Description,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayTimeMinutes,
    decimal? BggRating,
    string? CoverImageUrl,
    List<string>? Categories,
    List<string>? Mechanics,
    int BggId
) : IRequest<Guid>;
