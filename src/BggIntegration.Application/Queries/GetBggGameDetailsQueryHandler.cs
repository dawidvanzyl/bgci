using BggIntegration.Domain.Interfaces;
using BggIntegration.Domain.Models;
using MediatR;

namespace BggIntegration.Application.Queries;

public class GetBggGameDetailsQueryHandler : IRequestHandler<GetBggGameDetailsQuery, BggGameDetails?>
{
    private readonly IBggClient _bggClient;

    public GetBggGameDetailsQueryHandler(IBggClient bggClient)
    {
        _bggClient = bggClient;
    }

    public async Task<BggGameDetails?> Handle(GetBggGameDetailsQuery request, CancellationToken cancellationToken)
    {
        return await _bggClient.GetGameDetailsAsync(request.BggId, cancellationToken);
    }
}
