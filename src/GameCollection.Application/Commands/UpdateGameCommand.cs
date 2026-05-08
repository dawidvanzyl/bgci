using MediatR;

namespace GameCollection.Application.Commands;

public record UpdateGameCommand(
    Guid Id,
    string Name,
    int? Year,
    string? Description,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayTimeMinutes,
    int? MinPlayTimeMinutes,
    int? MaxPlayTimeMinutes,
    decimal? BggRating,
    decimal? BggWeight,
    int? MinAge,
    int? BestPlayerCountMin,
    int? BestPlayerCountMax,
    string? CoverImageUrl,
    List<string>? Categories,
    List<string>? Mechanics,
    List<string>? Designers,
    List<string>? Artists,
    List<string>? Publishers,
    List<string>? Subdomains
) : IRequest;
