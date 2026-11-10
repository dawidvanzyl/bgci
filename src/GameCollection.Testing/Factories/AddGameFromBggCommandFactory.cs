using GameCollection.Application.Commands;

namespace GameCollection.Testing.Factories;

/// <summary>
/// Builds a valid <see cref="AddGameFromBggCommand"/>. Optional fields default to <c>null</c>.
/// </summary>
public static class AddGameFromBggCommandFactory
{
	public static AddGameFromBggCommand Create(
		string name = "Catan",
		int bggId = CollectedGameFactory.DefaultBggId,
		bool skipBggWrite = false,
		long? bggCollId = null,
		Guid? parentGameId = null)
	{
		return new AddGameFromBggCommand(
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
			Categories: null,
			Mechanics: null,
			Designers: null,
			Artists: null,
			Publishers: null,
			Subdomains: null,
			BggId: bggId,
			BggCollId: bggCollId,
			SkipBggWrite: skipBggWrite,
			ParentGameId: parentGameId);
	}
}