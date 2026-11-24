using Jobs.Application.Commands;
using Jobs.Application.Models;
using Jobs.Worker.Jobs;
using MediatR;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Shouldly;
using Xunit;

namespace Jobs.Worker.Tests;

public sealed class BggJobTests
{
	private readonly Mock<IMediator> _mediator = new();
	private readonly RunningJobs _runningJobs = new(new FakeTimeProvider());

	[Fact]
	[Trait("AC", "112UC6")]
	public async Task RunAsync_AddWaitsForAnEarlierRequest_ThrowsTheWaitException()
	{
		_mediator
			.Setup(m => m.Send(It.IsAny<RunBggAddCommand>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(BggJobOutcome.WaitingForEarlierRequest);

		await Should.ThrowAsync<WaitForEarlierRequestException>(
			() => new BggAddJob(_mediator.Object, _runningJobs).RunAsync(Guid.NewGuid(), null, CancellationToken.None));
	}

	[Fact]
	[Trait("AC", "112UC6")]
	public async Task RunAsync_RemoveWaitsForAnEarlierRequest_ThrowsTheWaitException()
	{
		_mediator
			.Setup(m => m.Send(It.IsAny<RunBggRemoveCommand>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(BggJobOutcome.WaitingForEarlierRequest);

		await Should.ThrowAsync<WaitForEarlierRequestException>(
			() => new BggRemoveJob(_mediator.Object, _runningJobs).RunAsync(Guid.NewGuid(), null, CancellationToken.None));
	}

	[Fact]
	[Trait("AC", "112UC6")]
	public async Task RunAsync_BothJobsFinish_ReturnWithoutThrowing()
	{
		_mediator
			.Setup(m => m.Send(It.IsAny<RunBggAddCommand>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(BggJobOutcome.Finished);
		_mediator
			.Setup(m => m.Send(It.IsAny<RunBggRemoveCommand>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(BggJobOutcome.Finished);

		await new BggAddJob(_mediator.Object, _runningJobs).RunAsync(Guid.NewGuid(), null, CancellationToken.None);
		await new BggRemoveJob(_mediator.Object, _runningJobs).RunAsync(Guid.NewGuid(), null, CancellationToken.None);
	}
}
