using GameCollection.Application.DTOs;

namespace BggIntegration.Testing.Factories;

/// <summary>
/// Builds a valid <see cref="CollectedGameDto"/>. By default the game came from BGG and its add was sent.
/// </summary>
public static class CollectedGameDtoFactory
{
	public static CollectedGameDto Create(
		int? bggId = 13,
		string? bggStatus = BggStatusNames.Sent,
		Guid? parentGameId = null)
	{
		return new CollectedGameDto(
			Id: Guid.NewGuid(),
			Name: "Catan",
			Year: 1995,
			Description: null,
			MinPlayers: 3,
			MaxPlayers: 4,
			PlayTimeMinutes: 90,
			MinPlayTimeMinutes: 60,
			MaxPlayTimeMinutes: 120,
			BggRating: 7.1m,
			BggWeight: 2.3m,
			MinAge: 10,
			BestPlayerCountMin: 3,
			BestPlayerCountMax: 4,
			CoverImageUrl: null,
			Categories: [],
			Mechanics: [],
			Designers: [],
			Artists: [],
			Publishers: [],
			Subdomains: [],
			BggId: bggId,
			AddedAt: DateTime.UtcNow,
			UpdatedAt: DateTime.UtcNow,
			IsBggSourced: bggId.HasValue,
			ParentGameId: parentGameId,
			ExpansionCount: 0,
			BggStatus: bggStatus);
	}
}