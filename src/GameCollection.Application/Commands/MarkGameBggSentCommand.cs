using MediatR;

namespace GameCollection.Application.Commands;

public record MarkGameBggSentCommand(Guid GameId, long BggCollId) : IRequest<bool>;