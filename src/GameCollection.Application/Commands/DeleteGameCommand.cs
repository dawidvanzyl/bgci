using MediatR;

namespace GameCollection.Application.Commands;

public record DeleteGameCommand(Guid Id) : IRequest;
