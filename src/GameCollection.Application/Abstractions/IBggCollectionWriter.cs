namespace GameCollection.Application.Abstractions;

/// <summary>
/// Anti-Corruption Layer: abstracts BGG collection write operations so that
/// GameCollection.Application does not depend on BggIntegration directly.
/// The implementation (registered by BggIntegration.Infrastructure) handles
/// username resolution, reports <see cref="BggWriteOutcome.Skipped"/> when BGG writes
/// are not configured, and reports <see cref="BggWriteOutcome.Failed"/> instead of throwing
/// for a BGG failure or a writer timeout. It throws only when the token of the caller is cancelled.
/// </summary>
public interface IBggCollectionWriter
{
	Task<BggAddResult> AddToCollectionAsync(int bggId, string name, CancellationToken cancellationToken = default);

	Task<BggWriteOutcome> RemoveFromCollectionAsync(long? bggCollId, CancellationToken cancellationToken = default);
}