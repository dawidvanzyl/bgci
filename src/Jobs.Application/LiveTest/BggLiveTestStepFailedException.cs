namespace Jobs.Application.LiveTest;

internal sealed class BggLiveTestStepFailedException : Exception
{
	public BggLiveTestStepFailedException(BggLiveTestFailure failure)
		: base(failure.Message)
	{
		Failure = failure;
	}

	public BggLiveTestFailure Failure { get; }
}