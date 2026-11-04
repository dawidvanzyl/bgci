using BggIntegration.Domain.Models;

namespace BggIntegration.Testing.Factories;

/// <summary>
/// Builds a valid <see cref="BggGameDetails"/>. Tests state only the values that matter.
/// </summary>
public static class BggGameDetailsFactory
{
	public static BggGameDetails Create(
		int bggId = 13,
		string name = "Catan",
		string? thumbnailUrl = null,
		string? imageUrl = null)
	{
		return new BggGameDetails(
			BggId: bggId,
			Name: name,
			Year: 1995,
			Description: null,
			MinPlayers: 3,
			MaxPlayers: 4,
			PlayTimeMinutes: 90,
			MinPlayTimeMinutes: 60,
			MaxPlayTimeMinutes: 120,
			AverageRating: 7.1m,
			AverageWeight: 2.3m,
			MinAge: 10,
			BestPlayerCountMin: 3,
			BestPlayerCountMax: 4,
			ThumbnailUrl: thumbnailUrl,
			ImageUrl: imageUrl,
			Categories: [],
			Mechanics: [],
			Designers: [],
			Artists: [],
			Publishers: [],
			Subdomains: [],
			ParentBggIds: []);
	}
}