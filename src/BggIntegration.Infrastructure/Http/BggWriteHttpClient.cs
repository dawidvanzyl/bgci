using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace BggIntegration.Infrastructure.Http;

public sealed partial class BggWriteHttpClient : IBggWriteClient
{
	private const string LoginStep = "login";
	private const string AddStep = "add";
	private const string RemoveStep = "remove";
	private const string LookupStep = "collection item id lookup";
	private const int LookupAttempts = 5;
	private const int SummaryLength = 200;
	private const string Redacted = "***";

	private readonly HttpClient _http;
	private readonly BggSession _session;
	private readonly BggSettings _settings;
	private readonly TimeProvider _timeProvider;
	private readonly ILogger<BggWriteHttpClient> _logger;

	public BggWriteHttpClient(
		HttpClient http,
		BggSession session,
		IOptions<BggSettings> settings,
		TimeProvider timeProvider,
		ILogger<BggWriteHttpClient> logger)
	{
		_http = http;
		_session = session;
		_settings = settings.Value;
		_timeProvider = timeProvider;
		_logger = logger;
	}

	[GeneratedRegex("just a moment|security verification|cf-chl", RegexOptions.IgnoreCase)]
	private static partial Regex ChallengePattern();

	[GeneratedRegex(@"\s+")]
	private static partial Regex Whitespace();

	public async Task<long> AddToCollectionAsync(int bggId, string name, CancellationToken cancellationToken = default)
	{
		// Checked before any request, so a missing token cannot leave an item on BGG with no collection item id
		EnsureBearerToken();

		var cookie = await WriteWithSessionAsync(
			AddStep,
			cookie => BuildAddRequest(bggId, name, cookie),
			"Item saved",
			cancellationToken);

		return await LookUpCollectionItemIdAsync(bggId, cookie, true, cancellationToken)
			?? throw new BggWriteException(
				LookupStep,
				$"BGG {LookupStep} found no collection item id for bggId {bggId} after add.");
	}

	public async Task<long?> FindCollectionItemIdAsync(int bggId, CancellationToken cancellationToken = default)
	{
		EnsureBearerToken();
		return await LookUpCollectionItemIdAsync(bggId, null, false, cancellationToken);
	}

	public async Task RemoveFromCollectionAsync(long collId, CancellationToken cancellationToken = default)
	{
		await WriteWithSessionAsync(
			RemoveStep,
			cookie => BuildRemoveRequest(collId, cookie),
			"Item removed from collection",
			cancellationToken);
	}

	private async Task<string> WriteWithSessionAsync(
		string step,
		Func<string, HttpRequestMessage> buildRequest,
		string expectedMessage,
		CancellationToken cancellationToken)
	{
		var cookie = await _session.GetAsync(null, LoginAsync, cancellationToken);
		var result = await SendAsync(step, buildRequest(cookie), cancellationToken);

		if (IsRejectedSession(result.Status))
		{
			cookie = await _session.GetAsync(cookie, LoginAsync, cancellationToken);
			result = await SendAsync(step, buildRequest(cookie), cancellationToken);
		}

		return result.Status != HttpStatusCode.OK || ParseMessage(result.Body) != expectedMessage
			? throw new BggWriteException(
				step,
				$"BGG {step} failed with status {(int)result.Status}: {Describe(result.Body, cookie)}")
			: cookie;
	}

	private async Task<string> LoginAsync(CancellationToken cancellationToken)
	{
		var request = new HttpRequestMessage(HttpMethod.Post, BggApiEndpoints.Login)
		{
			Content = JsonContent(new
			{
				credentials = new { username = _settings.Username, password = _settings.Password },
			}),
		};
		request.Headers.TryAddWithoutValidation("Accept", "application/json");

		var result = await SendAsync(LoginStep, request, cancellationToken);

		if (result.Status != HttpStatusCode.NoContent)
		{
			throw new BggWriteException(
				LoginStep,
				$"BGG login failed with status {(int)result.Status}: {Describe(result.Body, null)}");
		}

		var cookies = new List<KeyValuePair<string, string>>();
		foreach (var setCookie in result.SetCookies)
		{
			var pair = setCookie.Split(';')[0];
			var separator = pair.IndexOf('=');
			if (separator > 0)
			{
				SetCookie(cookies, pair[..separator], pair[(separator + 1)..]);
			}
		}

		if (cookies.Count == 0)
		{
			throw new BggWriteException(LoginStep, "BGG login returned no session cookies.");
		}

		SetCookie(cookies, "bggusername", _settings.Username);

		return string.Join("; ", cookies.Select(c => $"{c.Key}={c.Value}"));
	}

	private static void SetCookie(List<KeyValuePair<string, string>> cookies, string name, string value)
	{
		var entry = new KeyValuePair<string, string>(name, value);
		var index = cookies.FindIndex(c => c.Key == name);
		if (index >= 0)
		{
			cookies[index] = entry;
		}
		else
		{
			cookies.Add(entry);
		}
	}

	private void EnsureBearerToken()
	{
		if (string.IsNullOrWhiteSpace(_settings.BearerToken))
		{
			throw new BggWriteException(LookupStep, "BGG collection item id lookup needs a bearer token.");
		}
	}

	private async Task<long?> LookUpCollectionItemIdAsync(int bggId, string? cookie, bool retryWhenAbsent, CancellationToken cancellationToken)
	{
		var url = string.Format(
			CultureInfo.InvariantCulture,
			BggApiEndpoints.CollectionItemLookup,
			Uri.EscapeDataString(_settings.Username),
			bggId);

		var answered = false;
		for (var attempt = 1; attempt <= LookupAttempts; attempt++)
		{
			if (attempt > 1)
			{
				await Task.Delay(TimeSpan.FromSeconds(attempt * 2), _timeProvider, cancellationToken);
			}

			var request = new HttpRequestMessage(HttpMethod.Get, url);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.BearerToken.Trim());

			var result = await SendAsync(LookupStep, request, cancellationToken);

			if (result.Status == HttpStatusCode.Accepted)
			{
				continue;
			}

			if (result.Status != HttpStatusCode.OK)
			{
				throw new BggWriteException(
					LookupStep,
					$"BGG {LookupStep} failed with status {(int)result.Status}: {Describe(result.Body, cookie)}");
			}

			answered = true;
			var collId = FindCollectionItemId(result.Body, bggId);
			if (collId is not null)
			{
				return collId.Value;
			}

			if (!retryWhenAbsent)
			{
				return null;
			}
		}

		return answered || retryWhenAbsent
			? null
			: throw new BggWriteException(LookupStep, $"BGG {LookupStep} was not ready for bggId {bggId}.");
	}

	private async Task<StepResult> SendAsync(string step, HttpRequestMessage request, CancellationToken cancellationToken)
	{
		HttpResponseMessage response;
		try
		{
			response = await _http.SendAsync(request, cancellationToken);
		}
		catch (HttpRequestException ex)
		{
			throw new BggWriteException(step, $"BGG {step} request failed.", ex);
		}

		using (response)
		{
			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			var setCookies = response.Headers.TryGetValues("Set-Cookie", out var values)
				? values.ToList()
				: [];

			_logger.LogInformation(
				"BGG {Step}: {Method} {Path} -> {Status}",
				step,
				request.Method,
				PathOf(request),
				(int)response.StatusCode);

			// Runs before any status check, so a challenge page on a 403 is not read as a rejected session
			return ChallengePattern().IsMatch(body)
				? throw new BggWriteException(step, $"BGG Cloudflare challenge during {step}.")
				: new StepResult(response.StatusCode, body, setCookies);
		}
	}

	private static HttpRequestMessage BuildAddRequest(int bggId, string name, string cookie)
	{
		var request = new HttpRequestMessage(HttpMethod.Post, BggApiEndpoints.CollectionItem)
		{
			Content = JsonContent(new
			{
				item = new
				{
					collid = 0,
					pp_currency = "USD",
					cv_currency = "USD",
					objecttype = "thing",
					objectid = bggId.ToString(CultureInfo.InvariantCulture),
					status = new { own = true },
					objectname = name,
					acquisitiondate = (string?)null,
					invdate = (string?)null,
				},
			}),
		};
		AddWriteHeaders(request, cookie);
		return request;
	}

	private static HttpRequestMessage BuildRemoveRequest(long collId, string cookie)
	{
		var request = new HttpRequestMessage(
			HttpMethod.Delete,
			string.Format(CultureInfo.InvariantCulture, BggApiEndpoints.CollectionItemById, collId));
		AddWriteHeaders(request, cookie);
		return request;
	}

	private static void AddWriteHeaders(HttpRequestMessage request, string cookie)
	{
		request.Headers.TryAddWithoutValidation("Accept", "application/json");
		request.Headers.TryAddWithoutValidation("X-KL-Ajax-Request", "Ajax_Request");
		request.Headers.TryAddWithoutValidation("Cookie", cookie);
	}

	private static StringContent JsonContent(object body)
	{
		var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8);
		content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
		return content;
	}

	private static bool IsRejectedSession(HttpStatusCode status) =>
		status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

	private static string PathOf(HttpRequestMessage request)
	{
		var path = (request.RequestUri?.OriginalString ?? string.Empty).Split('?')[0];
		return path.StartsWith('/') ? path : "/" + path;
	}

	private static string? ParseMessage(string body)
	{
		try
		{
			using var document = JsonDocument.Parse(body);
			return TextOf(document.RootElement, "message");
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private static long? FindCollectionItemId(string body, int bggId)
	{
		try
		{
			var expected = bggId.ToString(CultureInfo.InvariantCulture);
			var item = XDocument.Parse(body).Descendants("item")
				.FirstOrDefault(e => (string?)e.Attribute("objectid") == expected);
			return long.TryParse((string?)item?.Attribute("collid"), CultureInfo.InvariantCulture, out var collId) && collId > 0
				? collId
				: null;
		}
		catch (XmlException)
		{
			return null;
		}
	}

	private string Describe(string body, string? cookie) => Redact(Summarise(Redact(body, cookie)), cookie);

	private static string Summarise(string body)
	{
		try
		{
			using var document = JsonDocument.Parse(body);
			var root = document.RootElement;
			var message = TextOf(root, "message") ?? NestedMessage(root) ?? TextOf(root, "error");
			if (!string.IsNullOrEmpty(message))
			{
				return message;
			}
		}
		catch (JsonException)
		{
			// Not JSON, so the raw text is the summary
		}

		var trimmed = Whitespace().Replace(body, " ").Trim();
		return trimmed.Length == 0 ? "(empty body)" : trimmed[..Math.Min(SummaryLength, trimmed.Length)];
	}

	private static string? NestedMessage(JsonElement root) =>
		root.ValueKind == JsonValueKind.Object
			&& root.TryGetProperty("errors", out var errors)
				? TextOf(errors, "message")
				: null;

	private static string? TextOf(JsonElement element, string property)
	{
		return element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value)
			? null
			: value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
	}

	private string Redact(string text, string? cookie)
	{
		var secrets = new List<string>();
		if (!string.IsNullOrEmpty(_settings.Password))
		{
			secrets.Add(_settings.Password);
		}

		if (cookie is not null)
		{
			foreach (var pair in cookie.Split("; "))
			{
				var separator = pair.IndexOf('=');
				if (separator > 0 && separator < pair.Length - 1 && pair[..separator] != "bggusername")
				{
					secrets.Add(pair[(separator + 1)..]);
				}
			}
		}

		foreach (var secret in secrets.OrderByDescending(s => s.Length))
		{
			text = text.Replace(secret, Redacted, StringComparison.Ordinal);
		}

		return text;
	}

	private sealed record StepResult(HttpStatusCode Status, string Body, IReadOnlyList<string> SetCookies);
}