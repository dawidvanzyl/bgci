using GameCollection.Domain.Aggregates;
using GameCollection.Domain.ValueObjects;

namespace GameCollection.Domain.Repositories;

public interface ICollectedGameRepository
{
    Task<CollectedGame?> GetByIdAsync(GameId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CollectedGame>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(CollectedGame game, CancellationToken cancellationToken = default);

    Task UpdateAsync(CollectedGame game, CancellationToken cancellationToken = default);

    Task DeleteAsync(GameId id, CancellationToken cancellationToken = default);
}
