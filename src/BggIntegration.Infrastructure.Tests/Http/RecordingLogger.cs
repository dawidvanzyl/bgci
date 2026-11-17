using Microsoft.Extensions.Logging;

namespace BggIntegration.Infrastructure.Tests.Http;

public sealed class RecordingLogger<T> : ILogger<T>
{
	private readonly List<string> _entries = [];

	public IReadOnlyList<string> Entries
	{
		get
		{
			lock (_entries)
			{
				return _entries.ToList();
			}
		}
	}

	public IDisposable? BeginScope<TState>(TState state)
		where TState : notnull => null;

	public bool IsEnabled(LogLevel logLevel) => true;

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		lock (_entries)
		{
			_entries.Add(formatter(state, exception));
			if (exception is not null)
			{
				_entries.Add(exception.ToString());
			}
		}
	}
}