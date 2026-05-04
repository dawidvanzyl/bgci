using MediatR;

namespace GameCollection.Application.Commands;

public record AddGameManuallyCommand(
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
    Guid? ParentGameId = null
) : IRequest<Guid>;
