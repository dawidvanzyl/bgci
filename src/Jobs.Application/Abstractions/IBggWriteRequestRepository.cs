using Jobs.Application.Models;

namespace Jobs.Application.Abstractions;

public interface IBggWriteRequestRepository
{
	Task<BggWriteRequest?> GetAsync(Guid requestId, CancellationToken cancellationToken = default);

	Task<int?> StartAttemptAsync(Guid requestId, DateTimeOffset at, CancellationToken cancellationToken = default);

	Task<bool> HasUnfinishedEarlierRequestAsync(Guid requestId, CancellationToken cancellationToken = default);

	Task CompleteAsync(BggWriteCompletion completion, CancellationToken cancellationToken = default);
}