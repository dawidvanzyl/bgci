using GameCollection.Application.Commands;

namespace GameCollection.Testing.Factories;

/// <summary>
/// Builds a valid <see cref="AddGameManuallyCommand"/>. Optional fields default to <c>null</c>.
/// </summary>
public static class AddGameManuallyCommandFactory
{
	public static AddGameManuallyCommand Create(
		string name = "Catan",
		int? minPlayers = 2,
		int? maxPlayers = 4,
		int? playTimeMinutes = 60)
	{
		return new AddGameManuallyCommand(
			Name: name,
			Year: null,
			Description: null,
			MinPlayers: minPlayers,
			MaxPlayers: maxPlayers,
			PlayTimeMinutes: playTimeMinutes,
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
			Subdomains: null);
	}
}