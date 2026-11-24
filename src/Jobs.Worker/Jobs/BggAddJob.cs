using Hangfire.Server;
using Jobs.Application.Commands;
using Jobs.Infrastructure.Jobs;
using MediatR;

namespace Jobs.Worker.Jobs;

public sealed class BggAddJob : IBggAddJob
{
	private readonly IMediator _mediator;
	private readonly IRunningJobs _runningJobs;

	public BggAddJob(IMediator mediator, IRunningJobs runningJobs)
	{
		_mediator = mediator;
		_runningJobs = runningJobs;
	}

	public async Task RunAsync(Guid requestId, PerformContext? context, CancellationToken cancellationToken)
	{
		using (_runningJobs.Enter())
		{
			await _mediator.Send(new RunBggAddCommand(requestId, RetryCount.Of(context)), cancellationToken);
		}
	}
}
