using BggIntegration.Domain.Models;
using GameCollection.Application.Commands;

namespace BggIntegration.Application;

public static class BggMappings
{
	/// <summary>
	/// Anti-Corruption Layer: translates BGG domain concepts into GameCollection domain commands.
	/// </summary>
	public static AddGameFromBggCommand ToAddGameCommand(this BggGameDetails details) =>
        new(
            Name: details.Name,
            Year: details.Year,
            Description: details.Description,
            MinPlayers: details.MinPlayers,
            MaxPlayers: details.MaxPlayers,
            PlayTimeMinutes: details.PlayTimeMinutes,
            MinPlayTimeMinutes: details.MinPlayTimeMinutes,
            MaxPlayTimeMinutes: details.MaxPlayTimeMinutes,
            BggRating: details.AverageRating,
            BggWeight: details.AverageWeight,
            MinAge: details.MinAge,
            BestPlayerCountMin: details.BestPlayerCountMin,
            BestPlayerCountMax: details.BestPlayerCountMax,
            CoverImageUrl: details.ImageUrl ?? details.ThumbnailUrl,
            Categories: details.Categories,
            Mechanics: details.Mechanics,
            Designers: details.Designers,
            Artists: details.Artists,
            Publishers: details.Publishers,
            Subdomains: details.Subdomains,
            BggId: details.BggId
        );
}
