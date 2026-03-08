using BggIntegration.Domain.Models;
using MediatR;

namespace BggIntegration.Application.Queries;

public record GetBggGameDetailsQuery(int BggId) : IRequest<BggGameDetails?>;
