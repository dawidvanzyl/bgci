namespace GameCollection.Application.Abstractions;

/// <summary>
/// Anti-Corruption Layer: abstracts the queue of BGG collection writes so that
/// GameCollection.Application does not depend on the jobs service directly.
/// It also reads the state of the queue: the BGG ids that have a remove not yet finished.
/// The implementation creates the write request and its job together or not at all,
/// and throws when it cannot.
/// <see cref="QueueRemoveAsync"/> cancels the unfinished add of the same game, and queues a remove
/// only when that add may have reached BGG.
/// </summary>
public interface IBggWriteQueue
{
	Task QueueAddAsync(Guid gameId, int bggId, string name, CancellationToken cancellationToken = default);

	Task QueueRemoveAsync(Guid gameId, int bggId, long? bggCollId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<int>> GetPendingRemoveBggIdsAsync(CancellationToken cancellationToken = default);

	Task<bool> HasUnresolvedRemoveAsync(int bggId, CancellationToken cancellationToken = default);
}