using BggIntegration.Domain.Models;

namespace BggIntegration.Domain.Interfaces;

public interface IBggClient
{
	Task<IReadOnlyList<BggSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default);

	Task<BggGameDetails?> GetGameDetailsAsync(int bggId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<BggCollectionItem>> GetCollectionAsync(string username, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<BggCollectionItem>> GetExpansionCollectionAsync(string username, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<BggSearchResult>> GetExpansionsForGameAsync(int bggId, CancellationToken cancellationToken = default);
}
