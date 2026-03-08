using BggIntegration.Domain.Interfaces;
using BggIntegration.Domain.Models;
using GameCollection.Application.Commands;

namespace BggIntegration.Application.Translation;

public class BggTranslator : IBggTranslator
{
    public AddGameFromBggCommand ToAddGameCommand(BggGameDetails details) =>
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
