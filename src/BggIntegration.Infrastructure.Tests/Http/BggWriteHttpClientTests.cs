using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using System.Net;
using Xunit;
using static BggIntegration.Infrastructure.Tests.Http.FakeBggHandler;

namespace BggIntegration.Infrastructure.Tests.Http;

public sealed class BggWriteHttpClientTests
{
	private const string _username = "qa-user";
	private const string _password = "hunter2-secret";
	private const string _bearerToken = "qa-token";
	private const string _lookupStep = "collection item id lookup";

	private readonly FakeBggHandler _handler = new();
	private readonly FakeTimeProvider _time = new();
	private readonly RecordingLogger<BggWriteHttpClient> _logger = new();

	private static CancellationToken Token => TestContext.Current.CancellationToken;

	[Fact]
	[Trait("AC", "100UC1")]
	public async Task AddToCollectionAsync_NoSession_LogsInAddsAndLooksUp()
	{
		_handler.Script(
			"login",
			Login("SessionID=first; path=/", "bggusername=someone-else; path=/", "SessionID=second; path=/; HttpOnly", "bggpassword=pw-cookie"));
		_handler.Script("lookup", Lookup(13, 555));

		var collId = await CreateClient().AddToCollectionAsync(13, "Wingspan", Token);

		collId.ShouldBe(555);
		_handler.Requests.Select(r => r.Route).ShouldBe(["login", "add", "lookup"]);

		var login = _handler.Of("login").Single();
		login.ContentType.ShouldBe("application/json");
		login.Headers["Accept"].ShouldBe("application/json");
		login.Body.ShouldBe($$$"""{"credentials":{"username":"{{{_username}}}","password":"{{{_password}}}"}}""");

		var add = _handler.Of("add").Single();
		add.ContentType.ShouldBe("application/json");
		add.Headers["Accept"].ShouldBe("application/json");
		add.Headers["X-KL-Ajax-Request"].ShouldBe("Ajax_Request");
		add.Cookie.ShouldBe("SessionID=second; bggusername=qa-user; bggpassword=pw-cookie");
		add.Body.ShouldBe(
			"""{"item":{"collid":0,"pp_currency":"USD","cv_currency":"USD","objecttype":"thing","objectid":"13","status":{"own":true},"objectname":"Wingspan","acquisitiondate":null,"invdate":null}}""");

		var lookup = _handler.Of("lookup").Single();
		lookup.Query.ShouldBe("?username=qa-user&id=13&own=1");
		lookup.Headers["Authorization"].ShouldBe("Bearer qa-token");
	}

	[Fact]
	[Trait("AC", "100UC2")]
	public async Task Writes_OneSession_LogInOnceAndShareTheCookie()
	{
		var client = CreateClient();

		await client.AddToCollectionAsync(13, "Wingspan", Token);
		await client.RemoveFromCollectionAsync(555, Token);

		_handler.Count("login").ShouldBe(1);
		var remove = _handler.Of("remove").Single();
		remove.Path.ShouldBe("/api/collectionitem/555");
		remove.Headers["Accept"].ShouldBe("application/json");
		remove.Headers["X-KL-Ajax-Request"].ShouldBe("Ajax_Request");
		remove.Cookie.ShouldBe(_handler.Of("add").Single().Cookie);
	}

	[Fact]
	[Trait("AC", "100UC3")]
	public async Task Writes_StartedTogetherWithNoSession_LogInOnce()
	{
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_handler.BeforeLoginAnswer = () => release.Task;
		var client = CreateClient();

		var add = client.AddToCollectionAsync(13, "Wingspan", Token);
		var remove = client.RemoveFromCollectionAsync(555, Token);
		await WaitUntilAsync(() => _handler.Count("login") == 1);
		await SettleAsync();

		release.SetResult();
		await add;
		await remove;

		_handler.Count("login").ShouldBe(1);
		_handler.Of("add").Single().Cookie.ShouldBe("SessionID=session-1; bggusername=qa-user");
		_handler.Of("remove").Single().Cookie.ShouldBe("SessionID=session-1; bggusername=qa-user");
	}

	[Theory]
	[Trait("AC", "100UC4")]
	[InlineData(HttpStatusCode.Unauthorized)]
	[InlineData(HttpStatusCode.Forbidden)]
	public async Task AddToCollectionAsync_SessionRejected_LogsInAgainAndRepeatsTheAdd(HttpStatusCode rejection)
	{
		_handler.Script("add", Json(rejection, "Unauthorized"), Json(HttpStatusCode.OK, "Item saved"));
		_handler.Script("lookup", Lookup(13, 777));

		var collId = await CreateClient().AddToCollectionAsync(13, "Wingspan", Token);

		collId.ShouldBe(777);
		_handler.Count("login").ShouldBe(2);
		var adds = _handler.Of("add");
		adds.Count.ShouldBe(2);
		adds[0].Cookie.ShouldBe("SessionID=session-1; bggusername=qa-user");
		adds[1].Cookie.ShouldBe("SessionID=session-2; bggusername=qa-user");
	}

	[Theory]
	[Trait("AC", "100UC5")]
	[InlineData(HttpStatusCode.Unauthorized)]
	[InlineData(HttpStatusCode.Forbidden)]
	public async Task RemoveFromCollectionAsync_SessionRejected_LogsInAgainAndRepeatsTheRemove(HttpStatusCode rejection)
	{
		_handler.Script("remove", Json(rejection, "Unauthorized"), Json(HttpStatusCode.OK, "Item removed from collection"));

		await CreateClient().RemoveFromCollectionAsync(555, Token);

		_handler.Count("login").ShouldBe(2);
		var removes = _handler.Of("remove");
		removes.Count.ShouldBe(2);
		removes[0].Cookie.ShouldBe("SessionID=session-1; bggusername=qa-user");
		removes[1].Cookie.ShouldBe("SessionID=session-2; bggusername=qa-user");
	}

	[Theory]
	[Trait("AC", "100UC6")]
	[InlineData("add")]
	[InlineData("remove")]
	public async Task Write_RejectedAfterNewLogin_FailsWithTwoLoginsInTotal(string step)
	{
		var rejected = Json(HttpStatusCode.Unauthorized, "Unauthorized");
		_handler.Script(step, rejected, rejected, rejected);

		var exception = await Should.ThrowAsync<BggWriteException>(() => WriteAsync(CreateClient(), step));

		exception.Step.ShouldBe(step);
		_handler.Count("login").ShouldBe(2);
		_handler.Count(step).ShouldBe(2);
	}

	[Theory]
	[Trait("AC", "100UC7")]
	[InlineData("add")]
	[InlineData("remove")]
	public async Task Write_LoginRejected_FailsWithLoginStepAndSendsNoWrite(string step)
	{
		_handler.Script("login", Json(HttpStatusCode.BadRequest, "Invalid credentials"));

		var exception = await Should.ThrowAsync<BggWriteException>(() => WriteAsync(CreateClient(), step));

		exception.Step.ShouldBe("login");
		_handler.Requests.Select(r => r.Route).ShouldBe(["login"]);
	}

	[Fact]
	[Trait("AC", "100UC8")]
	public async Task AddToCollectionAsync_LookupAcceptedThenListed_WaitsBetweenAttempts()
	{
		_handler.Script("lookup", Answer(HttpStatusCode.Accepted), Lookup(13, 555));

		var add = CreateClient().AddToCollectionAsync(13, "Wingspan", Token);
		await WaitUntilAsync(() => _handler.Count("lookup") == 1);
		await SettleAsync();
		_time.Advance(TimeSpan.FromSeconds(3));
		await SettleAsync();

		add.IsCompleted.ShouldBeFalse();
		_handler.Count("lookup").ShouldBe(1);

		_time.Advance(TimeSpan.FromSeconds(1));

		(await add).ShouldBe(555);
		_handler.Count("lookup").ShouldBe(2);
	}

	[Theory]
	[Trait("AC", "100UC9")]
	[InlineData(HttpStatusCode.Accepted, "")]
	[InlineData(HttpStatusCode.OK, "<items totalitems=\"0\"/>")]
	public async Task AddToCollectionAsync_LookupNeverListsTheItem_FailsAfterFiveLookups(HttpStatusCode status, string body)
	{
		_handler.Script("lookup", Enumerable.Repeat(Answer(status, body), 10).ToArray());

		var exception = await Should.ThrowAsync<BggWriteException>(
			() => AdvanceUntilCompleteAsync(CreateClient().AddToCollectionAsync(13, "Wingspan", Token)));

		exception.Step.ShouldBe(_lookupStep);
		_handler.Count("lookup").ShouldBe(5);
	}

	[Fact]
	[Trait("AC", "111UC7")]
	public async Task FindCollectionItemIdAsync_ItemListed_ReturnsItsIdWithoutLoginOrWrite()
	{
		_handler.Script("lookup", Lookup(13, 555));

		var collId = await CreateClient().FindCollectionItemIdAsync(13, Token);

		collId.ShouldBe(555);
		_handler.Requests.Select(r => r.Route).ShouldBe(["lookup"]);
		_handler.Of("lookup").Single().Headers["Authorization"].ShouldBe("Bearer qa-token");
	}

	[Fact]
	[Trait("AC", "111UC7")]
	public async Task FindCollectionItemIdAsync_LookupAcceptedThenListed_WaitsAndReturnsTheId()
	{
		_handler.Script("lookup", Answer(HttpStatusCode.Accepted), Lookup(13, 555));

		var find = CreateClient().FindCollectionItemIdAsync(13, Token);
		await WaitUntilAsync(() => _handler.Count("lookup") == 1);
		await SettleAsync();
		_time.Advance(TimeSpan.FromSeconds(4));

		(await find).ShouldBe(555);
		_handler.Count("lookup").ShouldBe(2);
	}

	[Fact]
	[Trait("AC", "111UC7")]
	public async Task FindCollectionItemIdAsync_ItemNotListed_ReturnsNullAfterFiveLookups()
	{
		_handler.Script("lookup", Enumerable.Repeat(Answer(HttpStatusCode.OK, "<items totalitems=\"0\"/>"), 10).ToArray());

		var collId = await AdvanceUntilCompleteAsync(CreateClient().FindCollectionItemIdAsync(13, Token));

		collId.ShouldBeNull();
		_handler.Count("lookup").ShouldBe(5);
	}

	[Fact]
	[Trait("AC", "111UC7")]
	public async Task FindCollectionItemIdAsync_LookupAnswersError_FailsWithTheLookupStep()
	{
		_handler.Script("lookup", Json(HttpStatusCode.InternalServerError, "Boom"));

		var exception = await Should.ThrowAsync<BggWriteException>(
			() => CreateClient().FindCollectionItemIdAsync(13, Token));

		exception.Step.ShouldBe(_lookupStep);
	}

	[Fact]
	[Trait("AC", "111UC7")]
	public async Task FindCollectionItemIdAsync_NoBearerToken_FailsBeforeAnyRequest()
	{
		var client = new BggWriteHttpClient(
			new HttpClient(_handler) { BaseAddress = new Uri("http://bgg.test/") },
			new BggSession(),
			Options.Create(new BggSettings { Username = _username, Password = _password }),
			_time,
			_logger);

		var exception = await Should.ThrowAsync<BggWriteException>(() => client.FindCollectionItemIdAsync(13, Token));

		exception.Step.ShouldBe(_lookupStep);
		_handler.Requests.ShouldBeEmpty();
	}

	[Theory]
	[Trait("AC", "100UC10")]
	[InlineData("add", HttpStatusCode.InternalServerError, "Boom")]
	[InlineData("add", HttpStatusCode.OK, "Something else")]
	[InlineData("remove", HttpStatusCode.InternalServerError, "Boom")]
	[InlineData("remove", HttpStatusCode.OK, "Something else")]
	public async Task Write_UnexpectedAnswer_FailsWithTheStep(string step, HttpStatusCode status, string message)
	{
		_handler.Script(step, Json(status, message));

		var exception = await Should.ThrowAsync<BggWriteException>(() => WriteAsync(CreateClient(), step));

		exception.Step.ShouldBe(step);
		exception.Message.ShouldContain($"BGG {step} failed with status {(int)status}");
	}

	[Fact]
	[Trait("AC", "100UC10")]
	public async Task AddToCollectionAsync_LookupAnswersError_FailsAtOnceWithTheLookupStep()
	{
		_handler.Script("lookup", Json(HttpStatusCode.InternalServerError, "Boom"));

		var exception = await Should.ThrowAsync<BggWriteException>(
			() => CreateClient().AddToCollectionAsync(13, "Wingspan", Token));

		exception.Step.ShouldBe(_lookupStep);
		_handler.Count("lookup").ShouldBe(1);
	}

	[Theory]
	[Trait("AC", "100UC11")]
	[InlineData("login", "login", HttpStatusCode.OK, "<title>Just a moment...</title>")]
	[InlineData("add", "add", HttpStatusCode.Forbidden, "<html>Security Verification</html>")]
	[InlineData("lookup", _lookupStep, HttpStatusCode.OK, "<script src=\"/cdn-cgi/challenge-platform/cf-chl-bypass\"></script>")]
	[InlineData("remove", "remove", HttpStatusCode.OK, "<title>Just a moment...</title>")]
	public async Task Step_AnswersChallengePage_FailsWithTheStepAndNoNewLogin(string route, string step, HttpStatusCode status, string body)
	{
		_handler.Script(route, Answer(status, body));
		var client = CreateClient();

		var exception = await Should.ThrowAsync<BggWriteException>(
			() => route == "remove" ? client.RemoveFromCollectionAsync(555, Token) : client.AddToCollectionAsync(13, "Wingspan", Token));

		exception.Step.ShouldBe(step);
		exception.Message.ShouldContain("challenge");
		_handler.Count("login").ShouldBe(1);
	}

	[Fact]
	[Trait("AC", "100UC12")]
	public async Task Failures_BodiesRepeatSecrets_NeitherLogsNorMessagesHoldThem()
	{
		const string sessionValue = "sess-token-xyz";
		const string passwordCookieValue = "pw-cookie-abc";
		var leak = $"{_password} {sessionValue} {passwordCookieValue}";
		string[] secrets = [_password, sessionValue, passwordCookieValue];
		var exceptions = new List<Exception>();

		_handler.Script("login", Login($"SessionID={sessionValue}; path=/", $"bggpassword={passwordCookieValue}; path=/"));
		_handler.Script("add", Json(HttpStatusCode.InternalServerError, leak));
		_handler.Script("remove", Answer(HttpStatusCode.InternalServerError, $"<html>failed {leak}</html>"));
		_handler.Script("lookup", Json(HttpStatusCode.InternalServerError, leak));
		var client = CreateClient();

		exceptions.Add(await Should.ThrowAsync<BggWriteException>(() => client.AddToCollectionAsync(13, "Wingspan", Token)));
		exceptions.Add(await Should.ThrowAsync<BggWriteException>(() => client.RemoveFromCollectionAsync(555, Token)));
		_handler.Script("add", Json(HttpStatusCode.OK, "Item saved"));
		exceptions.Add(await Should.ThrowAsync<BggWriteException>(() => client.AddToCollectionAsync(13, "Wingspan", Token)));

		var rejectingHandler = new FakeBggHandler();
		rejectingHandler.Script("login", Json(HttpStatusCode.BadRequest, $"bad password {_password}"));
		exceptions.Add(await Should.ThrowAsync<BggWriteException>(
			() => CreateClient(rejectingHandler).AddToCollectionAsync(13, "Wingspan", Token)));

		foreach (var exception in exceptions)
		{
			for (var current = exception; current is not null; current = current.InnerException)
			{
				AssertHoldsNoSecret(current.Message, secrets);
			}
		}

		_logger.Entries.ShouldNotBeEmpty();
		foreach (var entry in _logger.Entries)
		{
			AssertHoldsNoSecret(entry, secrets);
		}
	}

	[Fact]
	[Trait("AC", "100UC12")]
	public async Task Failure_SecretCrossesTheSummaryCut_MessageHoldsNoPartOfIt()
	{
		var padding = new string('x', 195);
		_handler.Script("add", Answer(HttpStatusCode.InternalServerError, padding + _password));

		var exception = await Should.ThrowAsync<BggWriteException>(
			() => CreateClient().AddToCollectionAsync(13, "Wingspan", Token));

		exception.Message.ShouldNotContain(_password[..3]);
		exception.Message.ShouldContain("***");
	}

	private static void AssertHoldsNoSecret(string text, string[] secrets)
	{
		foreach (var secret in secrets)
		{
			text.ShouldNotContain(secret);
		}
	}

	private BggWriteHttpClient CreateClient(FakeBggHandler? handler = null)
	{
		return new BggWriteHttpClient(
			new HttpClient(handler ?? _handler) { BaseAddress = new Uri("http://bgg.test/") },
			new BggSession(),
			Options.Create(new BggSettings { Username = _username, Password = _password, BearerToken = _bearerToken }),
			_time,
			_logger);
	}

	private static Task WriteAsync(BggWriteHttpClient client, string step) =>
		step == "add"
			? client.AddToCollectionAsync(13, "Wingspan", Token)
			: client.RemoveFromCollectionAsync(555, Token);

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		for (var i = 0; i < 2000 && !condition(); i++)
		{
			await Task.Delay(1, Token);
		}

		condition().ShouldBeTrue();
	}

	private static async Task SettleAsync()
	{
		for (var i = 0; i < 50; i++)
		{
			await Task.Delay(1, Token);
		}
	}

	private async Task<T> AdvanceUntilCompleteAsync<T>(Task<T> task)
	{
		for (var i = 0; i < 2000 && !task.IsCompleted; i++)
		{
			_time.Advance(TimeSpan.FromSeconds(10));
			await Task.Delay(1, Token);
		}

		return await task;
	}
}