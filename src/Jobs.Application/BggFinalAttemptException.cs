namespace Jobs.Application;

public sealed class BggFinalAttemptException : Exception
{
	public BggFinalAttemptException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}