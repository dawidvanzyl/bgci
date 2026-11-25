using MediatR;

namespace Jobs.Application.Commands;

public record RunBggLiveTestCommand : IRequest;