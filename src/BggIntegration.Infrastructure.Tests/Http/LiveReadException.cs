namespace BggIntegration.Infrastructure.Tests.Http;

public sealed class LiveReadException : Exception
{
	public LiveReadException(LiveFailureKind kind, string message, Exception? innerException = null)
		: base(message, innerException)
	{
		Kind = kind;
	}

	public LiveFailureKind Kind { get; }
}