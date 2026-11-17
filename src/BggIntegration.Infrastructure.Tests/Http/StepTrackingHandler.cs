namespace BggIntegration.Infrastructure.Tests.Http;

public sealed class StepTrackingHandler : DelegatingHandler
{
	private string? _timedOutStep;

	public StepTrackingHandler(HttpMessageHandler innerHandler)
		: base(innerHandler)
	{
	}

	public string? TakeTimedOutStep()
	{
		var step = _timedOutStep;
		_timedOutStep = null;
		return step;
	}

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		try
		{
			return await base.SendAsync(request, cancellationToken);
		}
		catch (OperationCanceledException)
		{
			_timedOutStep = StepOf(request);
			throw;
		}
	}

	private static string StepOf(HttpRequestMessage request)
	{
		var path = request.RequestUri?.AbsolutePath ?? string.Empty;
		return path.Contains("login", StringComparison.OrdinalIgnoreCase)
			? LiveFailureClassifier.LoginStep
			: path.Contains("xmlapi2", StringComparison.OrdinalIgnoreCase)
				? LiveFailureClassifier.LookupStep
				: request.Method == HttpMethod.Delete
					? LiveFailureClassifier.RemoveStep
					: LiveFailureClassifier.AddStep;
	}
}