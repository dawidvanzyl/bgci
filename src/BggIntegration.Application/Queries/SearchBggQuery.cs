using BggIntegration.Domain.Models;
using MediatR;

namespace BggIntegration.Application.Queries;

public record SearchBggQuery(string Query) : IRequest<IReadOnlyList<BggSearchResult>>;
