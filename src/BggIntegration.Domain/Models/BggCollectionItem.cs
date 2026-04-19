namespace BggIntegration.Domain.Models;

public record BggCollectionItem(
	int BggId,
	long CollId,
	string Name,
	int? Year,
	string? ThumbnailUrl
);
