using Microsoft.Extensions.Logging;
using Xunit;

namespace BggIntegration.Infrastructure.Tests.Http;

public sealed class TestOutputLogger<T> : ILogger<T>
{
	private readonly ITestOutputHelper? _output;
	private readonly SecretRedactor _redactor;

	public TestOutputLogger(ITestOutputHelper? output, SecretRedactor redactor)
	{
		_output = output;
		_redactor = redactor;
	}

	public IDisposable? BeginScope<TState>(TState state)
		where TState : notnull => null;

	public bool IsEnabled(LogLevel logLevel) => true;

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		_output?.WriteLine(_redactor.Redact(formatter(state, exception)));
		if (exception is not null)
		{
			_output?.WriteLine(_redactor.Redact(exception.Message));
		}
	}
}