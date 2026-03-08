using GameCollection.Application.DTOs;
using MediatR;

namespace GameCollection.Application.Queries;

public record GetGameByIdQuery(Guid Id) : IRequest<CollectedGameDto?>;
