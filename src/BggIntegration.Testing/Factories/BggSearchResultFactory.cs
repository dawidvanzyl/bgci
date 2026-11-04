using BggIntegration.Domain.Models;

namespace BggIntegration.Testing.Factories;

public static class BggSearchResultFactory
{
	public static BggSearchResult Create(int bggId = 13, string name = "Catan", int? year = 1995)
	{
		return new BggSearchResult(bggId, name, year, ThumbnailUrl: null);
	}
}