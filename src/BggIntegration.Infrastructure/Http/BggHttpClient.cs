using System.Net;
using System.Xml.Linq;
using BggIntegration.Domain.Interfaces;
using BggIntegration.Domain.Models;

namespace BggIntegration.Infrastructure.Http;

public class BggHttpClient : IBggClient
{
    private readonly HttpClient _http;
    private const string BaseUrl = "https://boardgamegeek.com/xmlapi2";

    public BggHttpClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<BggSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/search?query={Uri.EscapeDataString(query)}&type=boardgame";
        var xml = await FetchXmlAsync(url, cancellationToken);
        return BggXmlParser.ParseSearchResults(xml);
    }

    public async Task<BggGameDetails?> GetGameDetailsAsync(int bggId, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/thing?id={bggId}&stats=1";
        var xml = await FetchXmlAsync(url, cancellationToken);
        return BggXmlParser.ParseGameDetails(xml, bggId);
    }

    private async Task<XDocument> FetchXmlAsync(string url, CancellationToken cancellationToken)
    {
        // BGG returns HTTP 202 when the request is queued — retry until 200
        const int maxRetries = 5;
        var delay = TimeSpan.FromSeconds(2);

        for (var i = 0; i < maxRetries; i++)
        {
            var response = await _http.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                await Task.Delay(delay, cancellationToken);
                delay *= 2;
                continue;
            }

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return XDocument.Parse(content);
        }

        throw new InvalidOperationException($"BGG API did not return a result after {maxRetries} retries for URL: {url}");
    }
}
