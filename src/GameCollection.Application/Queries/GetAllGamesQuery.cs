using GameCollection.Application.DTOs;
using MediatR;

namespace GameCollection.Application.Queries;

public record GetAllGamesQuery : IRequest<IReadOnlyList<CollectedGameDto>>;
