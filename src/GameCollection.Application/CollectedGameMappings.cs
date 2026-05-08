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
			MinPlayTimeMinutes: game.MinPlayTimeMinutes,
			MaxPlayTimeMinutes: game.MaxPlayTimeMinutes,
			BggRating: game.BggRating?.Value,
			BggWeight: game.BggWeight?.Value,
			MinAge: game.MinAge,
			BestPlayerCountMin: game.BestPlayerCountMin,
			BestPlayerCountMax: game.BestPlayerCountMax,
			CoverImageUrl: game.CoverImageUrl?.ToString(),
			Categories: game.Categories.ToList(),
			Mechanics: game.Mechanics.ToList(),
			Designers: game.Designers.ToList(),
			Artists: game.Artists.ToList(),
			Publishers: game.Publishers.ToList(),
			Subdomains: game.Subdomains.ToList(),
			BggId: game.BggId?.Value,
			AddedAt: game.AddedAt,
			UpdatedAt: game.UpdatedAt,
			IsBggSourced: game.IsBggSourced,
			ParentGameId: game.ParentGameId?.Value,
			ExpansionCount: game.ExpansionCount
		);
}
