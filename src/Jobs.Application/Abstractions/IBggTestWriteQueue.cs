namespace Jobs.Application.Abstractions;

/// <summary>
/// Queues BGG writes for the test account only. It takes no account, so the live test cannot name another one.
/// </summary>
public interface IBggTestWriteQueue
{
	Task<Guid> QueueAddAsync(int bggId, string name, CancellationToken cancellationToken = default);

	Task<Guid> QueueRemoveAsync(int bggId, long collectionItemId, CancellationToken cancellationToken = default);

	Task<bool> CancelAsync(Guid requestId, CancellationToken cancellationToken = default);
}