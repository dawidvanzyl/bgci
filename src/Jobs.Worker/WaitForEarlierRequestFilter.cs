using Hangfire.Common;
using Hangfire.States;
using Jobs.Worker.Jobs;

namespace Jobs.Worker;

internal sealed class WaitForEarlierRequestFilter : JobFilterAttribute, IElectStateFilter
{
	public const int FilterOrder = 10;

	private readonly TimeSpan _delay;

	public WaitForEarlierRequestFilter(TimeSpan delay)
	{
		_delay = delay;
		Order = FilterOrder;
	}

	public void OnStateElection(ElectStateContext context)
	{
		if (context.CandidateState is FailedState { Exception: WaitForEarlierRequestException })
		{
			context.CandidateState = new ScheduledState(_delay)
			{
				Reason = "Waiting for an earlier BGG write of the same game"
			};
		}
	}
}
