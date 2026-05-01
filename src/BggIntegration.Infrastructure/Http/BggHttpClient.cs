using BggIntegration.Domain.Interfaces;
using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure.Constants;
using Polly;
using Polly.Retry;
using System.Net;
using System.Xml.Linq;

namespace BggIntegration.Infrastructure.Http;

public class BggHttpClient : IBggClient
{
	private readonly HttpClient _http;

	// BGG returns HTTP 202 when the request is queued — retry with exponential backoff until 200
	private static readonly ResiliencePipeline<HttpResponseMessage> _bggPollingPipeline =
		new ResiliencePipelineBuilder<HttpResponseMessage>()
			.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
			{
				ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
					.HandleResult(r => r.StatusCode == HttpStatusCode.Accepted),
				MaxRetryAttempts = 5,
				BackoffType = DelayBackoffType.Exponential,
				Delay = TimeSpan.FromSeconds(2),
				UseJitter = false,
				OnRetry = args =>
				{
					args.Outcome.Result?.Dispose();
					return ValueTask.CompletedTask;
				},
			})
			.Build();

	public BggHttpClient(HttpClient http)
	{
		_http = http;
	}

	public async Task<IReadOnlyList<BggSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
	{
		var url = string.Format(BggApiEndpoints.Search, Uri.EscapeDataString(query));
		var xml = await FetchXmlAsync(url, cancellationToken);
		return BggXmlParser.ParseSearchResults(xml);
	}

	public async Task<BggGameDetails?> GetGameDetailsAsync(int bggId, CancellationToken cancellationToken = default)
	{
		var url = string.Format(BggApiEndpoints.GameDetail, bggId);
		var xml = await FetchXmlAsync(url, cancellationToken);
		return BggXmlParser.ParseGameDetails(xml, bggId);
	}

	public async Task<IReadOnlyList<BggCollectionItem>> GetCollectionAsync(string username, CancellationToken cancellationToken = default)
	{
		var url = string.Format(BggApiEndpoints.Collection, Uri.EscapeDataString(username));
		var xml = await FetchXmlAsync(url, cancellationToken);
		return BggXmlParser.ParseCollection(xml);
	}

	public async Task<IReadOnlyList<BggCollectionItem>> GetExpansionCollectionAsync(string username, CancellationToken cancellationToken = default)
	{
		var url = string.Format(BggApiEndpoints.ExpansionCollection, Uri.EscapeDataString(username));
		var xml = await FetchXmlAsync(url, cancellationToken);
		return BggXmlParser.ParseCollection(xml);
	}

	public async Task<IReadOnlyList<BggSearchResult>> GetExpansionsForGameAsync(int bggId, CancellationToken cancellationToken = default)
	{
		var url = string.Format(BggApiEndpoints.GameDetail, bggId);
		var xml = await FetchXmlAsync(url, cancellationToken);
		return BggXmlParser.ParseExpansionLinks(xml);
	}

	private async Task<XDocument> FetchXmlAsync(string url, CancellationToken cancellationToken)
	{
		using var response = await _bggPollingPipeline.ExecuteAsync(
			async ct => await _http.GetAsync(url, ct),
			cancellationToken);

		if (response.StatusCode == HttpStatusCode.Accepted)
		{
			throw new InvalidOperationException($"BGG API did not return a result after retries for URL: {url}");
		}

		response.EnsureSuccessStatusCode();
		var content = await response.Content.ReadAsStringAsync(cancellationToken);
		return XDocument.Parse(content);
	}
}
