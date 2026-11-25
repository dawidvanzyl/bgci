namespace Jobs.Application.Abstractions;

/// <summary>
/// Port to the BGG collection of one account. Every method throws when BGG fails.
/// </summary>
public interface IBggCollectionWriter
{
	Task<long> AddAsync(string account, int bggId, string name, CancellationToken cancellationToken = default);

	Task RemoveAsync(string account, long collectionItemId, CancellationToken cancellationToken = default);

	Task<long?> FindCollectionItemIdAsync(string account, int bggId, CancellationToken cancellationToken = default);
}