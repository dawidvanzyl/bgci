using GameCollection.Application.DTOs;

namespace Jobs.Testing.Factories;

public static class CollectedGameDtoFactory
{
	public static CollectedGameDto CreateFromBgg(
		Guid gameId,
		string bggStatus = "pending",
		string name = "Catan",
		int bggId = BggWriteRequestFactory.DefaultBggId)
	{
		return new CollectedGameDto(
			Id: gameId,
			Name: name,
			Year: null,
			Description: null,
			MinPlayers: null,
			MaxPlayers: null,
			PlayTimeMinutes: null,
			MinPlayTimeMinutes: null,
			MaxPlayTimeMinutes: null,
			BggRating: null,
			BggWeight: null,
			MinAge: null,
			BestPlayerCountMin: null,
			BestPlayerCountMax: null,
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
			IsBggSourced: true,
			ParentGameId: null,
			ExpansionCount: 0,
			BggStatus: bggStatus);
	}
}
