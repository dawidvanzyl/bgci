namespace BggIntegration.Domain.Models;

public record BggGameDetails(
    int BggId,
    string Name,
    int? Year,
    string? Description,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayTimeMinutes,
    int? MinPlayTimeMinutes,
    int? MaxPlayTimeMinutes,
    decimal? AverageRating,
    decimal? AverageWeight,
    int? MinAge,
    string? ThumbnailUrl,
    string? ImageUrl,
    List<string> Categories,
    List<string> Mechanics,
    List<string> Designers,
    List<string> Artists,
    List<string> Publishers,
    List<string> Subdomains,
    IReadOnlyList<int> ParentBggIds  // bggIds of base games this item is an expansion of (inbound links)
);
