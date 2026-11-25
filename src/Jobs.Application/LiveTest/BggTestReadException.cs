namespace Jobs.Application.LiveTest;

public sealed class BggTestReadException : Exception
{
	public BggTestReadException(BggLiveTestFailureKind kind, string message, Exception? innerException = null)
		: base(message, innerException)
	{
		Kind = kind;
	}

	public BggLiveTestFailureKind Kind { get; }
}