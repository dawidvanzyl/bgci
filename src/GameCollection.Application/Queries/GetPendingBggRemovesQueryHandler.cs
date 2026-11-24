using GameCollection.Application.Abstractions;
using MediatR;

namespace GameCollection.Application.Queries;

public class GetPendingBggRemovesQueryHandler : IRequestHandler<GetPendingBggRemovesQuery, IReadOnlyList<int>>
{
	private readonly IBggWriteQueue _bggWriteQueue;

	public GetPendingBggRemovesQueryHandler(IBggWriteQueue bggWriteQueue)
	{
		_bggWriteQueue = bggWriteQueue;
	}

	public Task<IReadOnlyList<int>> Handle(GetPendingBggRemovesQuery request, CancellationToken cancellationToken) =>
		_bggWriteQueue.GetPendingRemoveBggIdsAsync(cancellationToken);
}