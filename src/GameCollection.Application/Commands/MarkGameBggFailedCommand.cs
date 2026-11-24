using MediatR;

namespace GameCollection.Application.Commands;

public record MarkGameBggFailedCommand(Guid GameId) : IRequest<bool>;