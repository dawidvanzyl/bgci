namespace Jobs.Worker;

public sealed class RunningJobs : IRunningJobs
{
	private readonly object _gate = new();
	private readonly TimeProvider _timeProvider;
	private int _count;
	private TaskCompletionSource? _idle;

	public RunningJobs(TimeProvider timeProvider)
	{
		_timeProvider = timeProvider;
	}

	public IDisposable Enter()
	{
		lock (_gate)
		{
			_count++;
		}

		return new Exit(this);
	}

	public async Task WaitUntilIdleAsync(TimeSpan limit, CancellationToken cancellationToken = default)
	{
		Task idle;
		lock (_gate)
		{
			if (_count == 0)
			{
				return;
			}

			_idle ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			idle = _idle.Task;
		}

		await idle.WaitAsync(limit, _timeProvider, cancellationToken);
	}

	private void Leave()
	{
		lock (_gate)
		{
			_count--;
			if (_count == 0 && _idle is not null)
			{
				_idle.SetResult();
				_idle = null;
			}
		}
	}

	private sealed class Exit : IDisposable
	{
		private readonly RunningJobs _owner;
		private int _disposed;

		public Exit(RunningJobs owner)
		{
			_owner = owner;
		}

		public void Dispose()
		{
			if (Interlocked.Exchange(ref _disposed, 1) == 0)
			{
				_owner.Leave();
			}
		}
	}
}
