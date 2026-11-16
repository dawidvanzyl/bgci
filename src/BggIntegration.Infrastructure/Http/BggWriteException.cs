namespace BggIntegration.Infrastructure.Http;

public class BggWriteException : Exception
{
	public BggWriteException(string step, string message, Exception? innerException = null)
		: base(message, innerException)
	{
		Step = step;
	}

	public string Step { get; }
}