namespace GameCollection.Application.DTOs;

public record CollectedGameDto(
    Guid Id,
    string Name,
    int? Year,
    string? Description,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayTimeMinutes,
    decimal? BggRating,
    string? CoverImageUrl,
    List<string> Categories,
    List<string> Mechanics,
    int? BggId,
    DateTime AddedAt,
    DateTime UpdatedAt
);
