using BggIntegration.Domain.Interfaces;
using BggIntegration.Domain.Models;
using MediatR;

namespace BggIntegration.Application.Queries;

public class GetBggExpansionsQueryHandler : IRequestHandler<GetBggExpansionsQuery, IReadOnlyList<BggSearchResult>>
{
	private readonly IBggClient _bggClient;

	public GetBggExpansionsQueryHandler(IBggClient bggClient)
	{
		_bggClient = bggClient;
	}

	public async Task<IReadOnlyList<BggSearchResult>> Handle(GetBggExpansionsQuery request, CancellationToken cancellationToken)
	{
		return await _bggClient.GetExpansionsForGameAsync(request.BggId, cancellationToken);
	}
}
