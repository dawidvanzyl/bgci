namespace BggIntegration.Domain.Models;

public record BggGameDetails(
    int BggId,
    string Name,
    int? Year,
    string? Description,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayTimeMinutes,
    decimal? AverageRating,
    string? ThumbnailUrl,
    string? ImageUrl,
    List<string> Categories,
    List<string> Mechanics,
    IReadOnlyList<int> ParentBggIds  // bggIds of base games this item is an expansion of (inbound links)
);
