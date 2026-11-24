namespace GameCollection.Application.Abstractions;

/// <summary>
/// Anti-Corruption Layer: abstracts the queue of BGG collection writes so that
/// GameCollection.Application does not depend on the jobs service directly.
/// The implementation creates the write request and its job together or not at all,
/// and throws when it cannot.
/// </summary>
public interface IBggWriteQueue
{
	Task QueueAddAsync(Guid gameId, int bggId, string name, CancellationToken cancellationToken = default);

	Task QueueRemoveAsync(Guid gameId, int bggId, long? bggCollId, CancellationToken cancellationToken = default);
}