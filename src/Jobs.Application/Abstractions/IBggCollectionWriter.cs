namespace Jobs.Application.Abstractions;

/// <summary>
/// Port to the BGG collection. Every method throws when BGG fails.
/// </summary>
public interface IBggCollectionWriter
{
	Task<long> AddAsync(int bggId, string name, CancellationToken cancellationToken = default);

	Task RemoveAsync(long collectionItemId, CancellationToken cancellationToken = default);

	Task<long?> FindCollectionItemIdAsync(int bggId, CancellationToken cancellationToken = default);
}