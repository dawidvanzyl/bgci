namespace Api.Requests;

public record UpdateGameRequest(
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
    string? CoverImageUrl,
    List<string>? Categories,
    List<string>? Mechanics,
    List<string>? Designers,
    List<string>? Artists,
    List<string>? Publishers,
    List<string>? Subdomains
);
