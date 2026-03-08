namespace BggIntegration.Domain.Models;

public record BggSearchResult(
    int BggId,
    string Name,
    int? Year,
    string? ThumbnailUrl
);
