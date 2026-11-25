using Jobs.Application.Models;
using MediatR;

namespace Jobs.Application.Commands;

public record RunBggAddCommand(Guid RequestId, int RetryCount) : IRequest<BggJobOutcome>;