using BggIntegration.Domain.Models;

namespace BggIntegration.Testing.Factories;

/// <summary>
/// Builds a valid <see cref="BggCollectionItem"/>. Tests state only the values that matter.
/// </summary>
public static class BggCollectionItemFactory
{
	public static BggCollectionItem Create(int bggId = 13, long collId = 1000)
	{
		return new BggCollectionItem(
			BggId: bggId,
			CollId: collId,
			Name: "Catan",
			Year: 1995,
			ThumbnailUrl: null);
	}
}