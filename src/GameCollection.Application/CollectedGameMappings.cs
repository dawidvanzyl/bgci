using GameCollection.Application.DTOs;
using GameCollection.Domain.Aggregates;

namespace GameCollection.Application;

public static class CollectedGameMappings
{
    public static CollectedGameDto ToDto(this CollectedGame game) =>
        new(
            Id: game.Id.Value,
            Name: game.Name.Value,
            Year: game.Year,
            Description: game.Description,
            MinPlayers: game.PlayerCount?.Min,
            MaxPlayers: game.PlayerCount?.Max,
            PlayTimeMinutes: game.PlayTime?.Minutes,
            BggRating: game.BggRating?.Value,
            CoverImageUrl: game.CoverImageUrl?.ToString(),
            Categories: game.Categories.ToList(),
            Mechanics: game.Mechanics.ToList(),
            BggId: game.BggId?.Value,
            AddedAt: game.AddedAt,
            UpdatedAt: game.UpdatedAt
        );
}
