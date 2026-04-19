using BggIntegration.Domain.Interfaces;
using BggIntegration.Domain.Models;
using MediatR;

namespace BggIntegration.Application.Queries;

public class GetBggCollectionQueryHandler : IRequestHandler<GetBggCollectionQuery, IReadOnlyList<BggCollectionItem>>
{
	private readonly IBggClient _bggClient;

	public GetBggCollectionQueryHandler(IBggClient bggClient)
	{
		_bggClient = bggClient;
	}

	public async Task<IReadOnlyList<BggCollectionItem>> Handle(GetBggCollectionQuery request, CancellationToken cancellationToken)
	{
		return await _bggClient.GetCollectionAsync(request.Username, cancellationToken);
	}
}
