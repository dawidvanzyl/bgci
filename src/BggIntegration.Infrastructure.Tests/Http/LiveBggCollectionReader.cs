using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace BggIntegration.Infrastructure.Tests.Http;

public sealed partial class LiveBggCollectionReader
{
	private const int _attempts = 5;

	private readonly HttpClient _http;
	private readonly string _username;
	private readonly string _bearerToken;

	public LiveBggCollectionReader(HttpClient http, string username, string bearerToken)
	{
		_http = http;
		_username = username;
		_bearerToken = bearerToken;
	}

	[GeneratedRegex("just a moment|security verification|cf-chl", RegexOptions.IgnoreCase)]
	private static partial Regex ChallengePattern();

	public async Task<IReadOnlyList<long>> ListCollectionItemIdsAsync(int bggId, CancellationToken cancellationToken)
	{
		var url = string.Format(
			CultureInfo.InvariantCulture,
			"xmlapi2/collection?username={0}&id={1}",
			Uri.EscapeDataString(_username),
			bggId);

		for (var attempt = 1; attempt <= _attempts; attempt++)
		{
			if (attempt > 1)
			{
				await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
			}

			var (status, body) = await SendAsync(url, cancellationToken);

			if (ChallengePattern().IsMatch(body))
			{
				throw new LiveReadException(LiveFailureKind.Challenge, "BGG answered the collection read with a Cloudflare challenge.");
			}

			if ((int)status >= 500)
			{
				throw new LiveReadException(LiveFailureKind.Outage, $"BGG answered the collection read with status {(int)status}.");
			}

			if (status == HttpStatusCode.Accepted)
			{
				continue;
			}

			return status != HttpStatusCode.OK
				? throw new LiveReadException(LiveFailureKind.ReadFailed, $"BGG answered the collection read with status {(int)status}.")
				: ParseCollectionItemIds(body, bggId);
		}

		throw new LiveReadException(LiveFailureKind.ReadFailed, $"BGG still answered 202 to the collection read after {_attempts} attempts.");
	}

	private static List<long> ParseCollectionItemIds(string body, int bggId)
	{
		XDocument document;
		try
		{
			using var reader = XmlReader.Create(new StringReader(body), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
			document = XDocument.Load(reader);
		}
		catch (XmlException ex)
		{
			throw new LiveReadException(LiveFailureKind.ReadFailed, "BGG answered the collection read with a body that is not XML.", ex);
		}

		if (document.Root?.Name.LocalName != "items")
		{
			throw new LiveReadException(LiveFailureKind.ReadFailed, "BGG answered the collection read with XML that has no items element.");
		}

		var objectId = bggId.ToString(CultureInfo.InvariantCulture);
		return document.Root
			.Elements("item")
			.Where(item => (string?)item.Attribute("objectid") == objectId)
			.Select(item => long.TryParse((string?)item.Attribute("collid"), NumberStyles.None, CultureInfo.InvariantCulture, out var collId) ? collId : (long?)null)
			.OfType<long>()
			.ToList();
	}

	private async Task<(HttpStatusCode Status, string Body)> SendAsync(string url, CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, url);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _bearerToken.Trim());

		try
		{
			using var response = await _http.SendAsync(request, cancellationToken);
			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			return (response.StatusCode, body);
		}
		catch (HttpRequestException ex)
		{
			throw new LiveReadException(LiveFailureKind.Outage, "No answer from BGG to the collection read.", ex);
		}
	}
}