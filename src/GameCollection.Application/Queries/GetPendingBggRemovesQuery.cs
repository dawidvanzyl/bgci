using MediatR;

namespace GameCollection.Application.Queries;

public record GetPendingBggRemovesQuery : IRequest<IReadOnlyList<int>>;