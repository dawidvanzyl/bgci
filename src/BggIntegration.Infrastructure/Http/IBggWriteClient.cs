namespace BggIntegration.Infrastructure.Http;

public interface IBggWriteClient
{
	Task<long> AddToCollectionAsync(int bggId, string name, CancellationToken cancellationToken = default);

	Task RemoveFromCollectionAsync(long collId, CancellationToken cancellationToken = default);

	Task<long?> FindCollectionItemIdAsync(int bggId, CancellationToken cancellationToken = default);
}