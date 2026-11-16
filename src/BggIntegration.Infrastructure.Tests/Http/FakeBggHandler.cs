using System.Net;

namespace BggIntegration.Infrastructure.Tests.Http;

public sealed class FakeBggHandler : HttpMessageHandler
{
	private readonly object _gate = new();
	private readonly List<RecordedRequest> _requests = [];
	private readonly Dictionary<string, Queue<Func<HttpResponseMessage>>> _scripts = [];
	private int _logins;

	public Func<Task>? BeforeLoginAnswer { get; set; }

	public IReadOnlyList<RecordedRequest> Requests
	{
		get
		{
			lock (_gate)
			{
				return _requests.ToList();
			}
		}
	}

	public int Count(string route) => Requests.Count(r => r.Route == route);

	public IReadOnlyList<RecordedRequest> Of(string route) => Requests.Where(r => r.Route == route).ToList();

	public void Script(string route, params Func<HttpResponseMessage>[] answers)
	{
		lock (_gate)
		{
			_scripts[route] = new Queue<Func<HttpResponseMessage>>(answers);
		}
	}

	public static Func<HttpResponseMessage> Answer(HttpStatusCode status, string body = "") =>
		() => new HttpResponseMessage(status) { Content = new StringContent(body) };

	public static Func<HttpResponseMessage> Json(HttpStatusCode status, string message) =>
		Answer(status, $$"""{"message":"{{message}}"}""");

	public static Func<HttpResponseMessage> Login(params string[] setCookies) =>
		() =>
		{
			var response = new HttpResponseMessage(HttpStatusCode.NoContent);
			foreach (var setCookie in setCookies)
			{
				response.Headers.TryAddWithoutValidation("Set-Cookie", setCookie);
			}

			return response;
		};

	public static Func<HttpResponseMessage> Lookup(int bggId, long collId) =>
		Answer(
			HttpStatusCode.OK,
			$"""<items totalitems="2"><item objecttype="thing" objectid="1" collid="7"/><item objecttype="thing" objectid="{bggId}" collid="{collId}"/></items>""");

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
		var recorded = new RecordedRequest(
			request.Method.Method,
			request.RequestUri!.AbsolutePath,
			request.RequestUri.Query,
			body,
			request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase),
			request.Content?.Headers.ContentType?.ToString());

		lock (_gate)
		{
			_requests.Add(recorded);
		}

		if (recorded.Route == "login")
		{
			var login = Interlocked.Increment(ref _logins);
			if (BeforeLoginAnswer is not null)
			{
				await BeforeLoginAnswer();
			}

			return Next(recorded.Route, DefaultLogin(login));
		}

		return Next(recorded.Route, DefaultAnswer(recorded));
	}

	private HttpResponseMessage Next(string route, Func<HttpResponseMessage> fallback)
	{
		lock (_gate)
		{
			if (_scripts.TryGetValue(route, out var queue) && queue.Count > 0)
			{
				return queue.Dequeue()();
			}
		}

		return fallback();
	}

	private static Func<HttpResponseMessage> DefaultLogin(int login) => Login($"SessionID=session-{login}; path=/; HttpOnly");

	private static Func<HttpResponseMessage> DefaultAnswer(RecordedRequest request) => request.Route switch
	{
		"add" => Json(HttpStatusCode.OK, "Item saved"),
		"remove" => Json(HttpStatusCode.OK, "Item removed from collection"),
		"lookup" => Lookup(13, 555),
		_ => Answer(HttpStatusCode.NotFound),
	};
}