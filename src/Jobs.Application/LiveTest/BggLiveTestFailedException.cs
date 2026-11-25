namespace Jobs.Application.LiveTest;

public sealed class BggLiveTestFailedException : Exception
{
	public BggLiveTestFailedException(string message)
		: base(message)
	{
	}
}