using Jobs.Application.Models;
using MediatR;

namespace Jobs.Application.Commands;

public record RunBggRemoveCommand(Guid RequestId, int RetryCount) : IRequest<BggJobOutcome>;