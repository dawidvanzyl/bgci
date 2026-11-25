using Hangfire.Server;
using Jobs.Application.Commands;
using MediatR;

namespace Jobs.Worker.Jobs;

public sealed class BggLiveTestJob : IBggLiveTestJob
{
	private readonly IMediator _mediator;

	public BggLiveTestJob(IMediator mediator)
	{
		_mediator = mediator;
	}

	public Task RunAsync(PerformContext? context, CancellationToken cancellationToken) =>
		_mediator.Send(new RunBggLiveTestCommand(), cancellationToken);
}