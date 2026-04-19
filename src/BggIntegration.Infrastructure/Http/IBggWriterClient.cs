namespace BggIntegration.Infrastructure.Http;

public interface IBggWriterClient
{
	Task<long> AddToCollectionAsync(string username, int bggId, CancellationToken cancellationToken = default);

	Task RemoveFromCollectionAsync(string username, long collId, CancellationToken cancellationToken = default);
}
