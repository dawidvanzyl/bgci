using BggIntegration.Domain.Interfaces;
using BggIntegration.Domain.Models;
using MediatR;

namespace BggIntegration.Application.Queries;

public class SearchBggQueryHandler : IRequestHandler<SearchBggQuery, IReadOnlyList<BggSearchResult>>
{
    private readonly IBggClient _bggClient;

    public SearchBggQueryHandler(IBggClient bggClient)
    {
        _bggClient = bggClient;
    }

    public async Task<IReadOnlyList<BggSearchResult>> Handle(SearchBggQuery request, CancellationToken cancellationToken)
    {
		return string.IsNullOrWhiteSpace(request.Query)
			? Array.Empty<BggSearchResult>()
			: await _bggClient.SearchAsync(request.Query, cancellationToken);
	}
}
