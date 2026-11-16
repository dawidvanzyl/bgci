namespace BggIntegration.Infrastructure.Http;

public sealed class BggSession
{
	private readonly SemaphoreSlim _lock = new(1, 1);
	private string? _cookie;

	public async Task<string> GetAsync(
		string? rejectedCookie,
		Func<CancellationToken, Task<string>> login,
		CancellationToken cancellationToken)
	{
		await _lock.WaitAsync(cancellationToken);
		try
		{
			if (_cookie is null || _cookie == rejectedCookie)
			{
				_cookie = null;
				_cookie = await login(cancellationToken);
			}

			return _cookie;
		}
		finally
		{
			_lock.Release();
		}
	}
}
