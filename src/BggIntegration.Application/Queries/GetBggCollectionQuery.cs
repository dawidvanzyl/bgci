using BggIntegration.Domain.Models;
using MediatR;

namespace BggIntegration.Application.Queries;

public record GetBggCollectionQuery(string Username)
	: IRequest<IReadOnlyList<BggCollectionItem>>;
