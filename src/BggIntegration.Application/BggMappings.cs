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
            BggRating: details.AverageRating,
            CoverImageUrl: details.ImageUrl ?? details.ThumbnailUrl,
            Categories: details.Categories,
            Mechanics: details.Mechanics,
            BggId: details.BggId
        );
}
