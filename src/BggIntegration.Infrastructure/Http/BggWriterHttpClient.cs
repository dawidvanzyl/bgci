using BggIntegration.Infrastructure.Constants;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace BggIntegration.Infrastructure.Http;

public class BggWriterHttpClient : IBggWriterClient
{
	private readonly HttpClient _http;
	private readonly ILogger<BggWriterHttpClient> _logger;

	public BggWriterHttpClient(HttpClient http, ILogger<BggWriterHttpClient> logger)
	{
		_http = http;
		_logger = logger;
	}

	public async Task<long> AddToCollectionAsync(string username, int bggId, CancellationToken cancellationToken = default)
	{
		var response = await _http.PostAsJsonAsync(
			BggWriterApiEndpoints.AddToCollection,
			new { username, bggId },
			cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			_logger.LogWarning(
				"bgg-writer add failed for bggId {BggId} (user: {Username}). Status: {Status}. Body: {Body}",
				bggId, username, response.StatusCode, body);

			throw new HttpRequestException($"bgg-writer add failed with status {response.StatusCode}");
		}

		var result = await response.Content.ReadFromJsonAsync<AddResponse>(cancellationToken);
		return result?.CollId ?? 0;
	}

	public async Task RemoveFromCollectionAsync(string username, long collId, CancellationToken cancellationToken = default)
	{
		var response = await _http.PostAsJsonAsync(
			BggWriterApiEndpoints.RemoveFromCollection,
			new { username, collId },
			cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			_logger.LogWarning(
				"bgg-writer remove failed for collId {CollId} (user: {Username}). Status: {Status}. Body: {Body}",
				collId, username, response.StatusCode, body);

			throw new HttpRequestException($"bgg-writer remove failed with status {response.StatusCode}");
		}
	}

	private sealed record AddResponse(long CollId);
}
