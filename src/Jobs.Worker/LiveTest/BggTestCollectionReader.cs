using Jobs.Application.Abstractions;
using Jobs.Application.LiveTest;
using Jobs.Infrastructure.Jobs;
using Jobs.Worker.Bgg;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Jobs.Worker.LiveTest;

public sealed partial class BggTestCollectionReader : IBggTestCollectionReader
{
	private const int _attempts = 5;
	private static readonly TimeSpan _maxRetryDelay = TimeSpan.FromMinutes(1);

	private readonly IHttpClientFactory _httpClientFactory;
	private readonly IBggAccountClients _accounts;
	private readonly TimeProvider _timeProvider;

	public BggTestCollectionReader(IHttpClientFactory httpClientFactory, IBggAccountClients accounts, TimeProvider timeProvider)
	{
		_httpClientFactory = httpClientFactory;
		_accounts = accounts;
		_timeProvider = timeProvider;
	}

	[GeneratedRegex("just a moment|security verification|cf-chl", RegexOptions.IgnoreCase)]
	private static partial Regex ChallengePattern();

	public async Task<IReadOnlyList<long>> ListCollectionItemIdsAsync(int bggId, CancellationToken cancellationToken = default)
	{
		var settings = _accounts.Get(BggAccounts.Test).Settings;
		var url = string.Format(
			CultureInfo.InvariantCulture,
			"xmlapi2/collection?username={0}&id={1}",
			Uri.EscapeDataString(settings.Username),
			bggId);

		var delay = TimeSpan.Zero;
		var lastRetryStatus = HttpStatusCode.Accepted;
		for (var attempt = 1; attempt <= _attempts; attempt++)
		{
			if (attempt > 1)
			{
				await Task.Delay(delay, _timeProvider, cancellationToken);
			}

			var (status, body, retryAfter) = await SendAsync(url, settings.BearerToken, cancellationToken);

			if (ChallengePattern().IsMatch(body))
			{
				throw new BggTestReadException(BggLiveTestFailureKind.Challenge, "BGG answered the collection read with a Cloudflare challenge.");
			}

			if ((int)status >= 500)
			{
				throw new BggTestReadException(BggLiveTestFailureKind.Outage, $"BGG answered the collection read with status {(int)status}.");
			}

			if (status is HttpStatusCode.Accepted or HttpStatusCode.TooManyRequests)
			{
				lastRetryStatus = status;
				delay = RetryDelay(status, attempt, retryAfter);
				continue;
			}

			return status != HttpStatusCode.OK
				? throw new BggTestReadException(BggLiveTestFailureKind.ReadFailed, $"BGG answered the collection read with status {(int)status}.")
				: ParseCollectionItemIds(body, bggId);
		}

		throw new BggTestReadException(BggLiveTestFailureKind.ReadFailed, $"BGG still answered {(int)lastRetryStatus} to the collection read after {_attempts} attempts.");
	}

	// BGG answers 202 while it builds the collection and 429 when it limits the rate. A Retry-After header wins over the backoff.
	private static TimeSpan RetryDelay(HttpStatusCode status, int attempt, TimeSpan? retryAfter)
	{
		var delay = retryAfter ?? TimeSpan.FromSeconds(attempt * (status == HttpStatusCode.TooManyRequests ? 10 : 2));
		return delay > _maxRetryDelay ? _maxRetryDelay : delay;
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
			throw new BggTestReadException(BggLiveTestFailureKind.ReadFailed, "BGG answered the collection read with a body that is not XML.", ex);
		}

		if (document.Root?.Name.LocalName != "items")
		{
			throw new BggTestReadException(BggLiveTestFailureKind.ReadFailed, "BGG answered the collection read with XML that has no items element.");
		}

		var objectId = bggId.ToString(CultureInfo.InvariantCulture);
		return document.Root
			.Elements("item")
			.Where(item => (string?)item.Attribute("objectid") == objectId)
			.Select(item => long.TryParse((string?)item.Attribute("collid"), NumberStyles.None, CultureInfo.InvariantCulture, out var collId) ? collId : (long?)null)
			.OfType<long>()
			.ToList();
	}

	private async Task<(HttpStatusCode Status, string Body, TimeSpan? RetryAfter)> SendAsync(string url, string bearerToken, CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, url);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken.Trim());

		try
		{
			using var http = _httpClientFactory.CreateClient(BggAccountClients.BggWriteClientName);
			using var response = await http.SendAsync(request, cancellationToken);
			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			var header = response.Headers.RetryAfter;
			var wait = header?.Delta ?? (header?.Date - _timeProvider.GetUtcNow());
			return (response.StatusCode, body, wait > TimeSpan.Zero ? wait : null);
		}
		catch (HttpRequestException ex)
		{
			throw new BggTestReadException(BggLiveTestFailureKind.Outage, "No answer from BGG to the collection read.", ex);
		}
	}
}