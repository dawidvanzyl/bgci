using BggIntegration.Domain.Models;
using MediatR;

namespace BggIntegration.Application.Queries;

public record GetBggExpansionsQuery(int BggId) : IRequest<IReadOnlyList<BggSearchResult>>;
