namespace Jobs.Application.Abstractions;

public interface IBggTestCollectionReader
{
	Task<IReadOnlyList<long>> ListCollectionItemIdsAsync(int bggId, CancellationToken cancellationToken = default);
}