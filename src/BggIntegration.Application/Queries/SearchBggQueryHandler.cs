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
        if (string.IsNullOrWhiteSpace(request.Query))
            return Array.Empty<BggSearchResult>();

        return await _bggClient.SearchAsync(request.Query, cancellationToken);
    }
}
