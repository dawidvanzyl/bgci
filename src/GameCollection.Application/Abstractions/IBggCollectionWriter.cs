namespace GameCollection.Application.Abstractions;

/// <summary>
/// Anti-Corruption Layer: abstracts BGG collection write operations so that
/// GameCollection.Application does not depend on BggIntegration directly.
/// The implementation (registered by BggIntegration.Infrastructure) handles
/// username resolution and no-ops gracefully when BGG is not configured.
/// </summary>
public interface IBggCollectionWriter
{
	Task<long?> AddToCollectionAsync(int bggId, CancellationToken cancellationToken = default);

	Task RemoveFromCollectionAsync(long? bggCollId, CancellationToken cancellationToken = default);
}
