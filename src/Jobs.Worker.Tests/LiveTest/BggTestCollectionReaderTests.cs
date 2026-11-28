using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure.Http;
using Jobs.Application.LiveTest;
using Jobs.Infrastructure.Jobs;
using Jobs.Worker.Bgg;
using Jobs.Worker.LiveTest;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Shouldly;
using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace Jobs.Worker.Tests.LiveTest;

public sealed class BggTestCollectionReaderTests
{
	private const string _collection = """<items><item objectid="13" collid="555" /><item objectid="99" collid="1" /></items>""";

	private readonly FakeTimeProvider _time = new();
	private readonly Queue<Func<HttpResponseMessage>> _responses = new();
	private int _calls;

	[Fact]
	[Trait("AC", "123UC1")]
	public async Task ListCollectionItemIdsAsync_Ok_ReturnsTheItemIdsOfTheGame()
	{
		Enqueue(HttpStatusCode.OK, _collection);

		var ids = await CreateReader().ListCollectionItemIdsAsync(13, TestContext.Current.CancellationToken);

		ids.ShouldBe([555L]);
	}

	[Fact]
	[Trait("AC", "123UC2")]
	public async Task ListCollectionItemIdsAsync_TooManyRequestsThenOk_WaitsAndRetries()
	{
		Enqueue(HttpStatusCode.TooManyRequests, string.Empty);
		Enqueue(HttpStatusCode.OK, _collection);

		var read = CreateReader().ListCollectionItemIdsAsync(13, TestContext.Current.CancellationToken);
		await WaitForCallsAsync(1);
		read.IsCompleted.ShouldBeFalse();
		_time.Advance(TimeSpan.FromSeconds(10));

		(await read).ShouldBe([555L]);
		_calls.ShouldBe(2);
	}

	[Fact]
	[Trait("AC", "123UC3")]
	public async Task ListCollectionItemIdsAsync_TooManyRequestsWithRetryAfter_WaitsForTheHeader()
	{
		Enqueue(HttpStatusCode.TooManyRequests, string.Empty, retryAfter: TimeSpan.FromSeconds(3));
		Enqueue(HttpStatusCode.OK, _collection);

		var read = CreateReader().ListCollectionItemIdsAsync(13, TestContext.Current.CancellationToken);
		await WaitForCallsAsync(1);
		_time.Advance(TimeSpan.FromSeconds(3));

		(await read).ShouldBe([555L]);
	}

	[Fact]
	[Trait("AC", "123UC4")]
	public async Task ListCollectionItemIdsAsync_RetryAfterIsHuge_CapsTheWait()
	{
		Enqueue(HttpStatusCode.TooManyRequests, string.Empty, retryAfter: TimeSpan.FromHours(1));
		Enqueue(HttpStatusCode.OK, _collection);

		var read = CreateReader().ListCollectionItemIdsAsync(13, TestContext.Current.CancellationToken);
		await WaitForCallsAsync(1);
		_time.Advance(TimeSpan.FromMinutes(1));

		(await read).ShouldBe([555L]);
	}

	[Fact]
	[Trait("AC", "123UC5")]
	public async Task ListCollectionItemIdsAsync_AlwaysTooManyRequests_ThrowsReadFailedAfterTheAttempts()
	{
		for (var i = 0; i < 5; i++)
		{
			Enqueue(HttpStatusCode.TooManyRequests, string.Empty);
		}

		var read = CreateReader().ListCollectionItemIdsAsync(13, TestContext.Current.CancellationToken);
		for (var call = 1; call <= 4; call++)
		{
			await WaitForCallsAsync(call);
			_time.Advance(TimeSpan.FromMinutes(1));
		}

		var exception = await Should.ThrowAsync<BggTestReadException>(() => read);
		exception.Kind.ShouldBe(BggLiveTestFailureKind.ReadFailed);
		exception.Message.ShouldContain("429");
		_calls.ShouldBe(5);
	}

	[Fact]
	[Trait("AC", "123UC6")]
	public async Task ListCollectionItemIdsAsync_AcceptedThenOk_StillRetries()
	{
		Enqueue(HttpStatusCode.Accepted, string.Empty);
		Enqueue(HttpStatusCode.OK, _collection);

		var read = CreateReader().ListCollectionItemIdsAsync(13, TestContext.Current.CancellationToken);
		await WaitForCallsAsync(1);
		_time.Advance(TimeSpan.FromSeconds(2));

		(await read).ShouldBe([555L]);
	}

	[Fact]
	[Trait("AC", "123UC7")]
	public async Task ListCollectionItemIdsAsync_ServerError_ThrowsOutageWithoutRetry()
	{
		Enqueue(HttpStatusCode.ServiceUnavailable, string.Empty);

		var exception = await Should.ThrowAsync<BggTestReadException>(() => CreateReader().ListCollectionItemIdsAsync(13, TestContext.Current.CancellationToken));

		exception.Kind.ShouldBe(BggLiveTestFailureKind.Outage);
		_calls.ShouldBe(1);
	}

	private void Enqueue(HttpStatusCode status, string body, TimeSpan? retryAfter = null) =>
		_responses.Enqueue(() =>
		{
			var response = new HttpResponseMessage(status) { Content = new StringContent(body) };
			if (retryAfter is { } wait)
			{
				response.Headers.RetryAfter = new RetryConditionHeaderValue(wait);
			}

			return response;
		});

	private async Task WaitForCallsAsync(int calls)
	{
		for (var i = 0; i < 200 && _calls < calls; i++)
		{
			await Task.Delay(5);
		}

		_calls.ShouldBeGreaterThanOrEqualTo(calls);
	}

	private BggTestCollectionReader CreateReader()
	{
		var factory = new Mock<IHttpClientFactory>();
		factory.Setup(f => f.CreateClient(BggAccountClients.BggWriteClientName))
			.Returns(() => new HttpClient(new StubHandler(this)) { BaseAddress = new Uri("https://boardgamegeek.com/") });

		var accounts = new Mock<IBggAccountClients>();
		accounts.Setup(a => a.Get(BggAccounts.Test))
			.Returns(new BggAccountClient(Mock.Of<IBggWriteClient>(), new BggSettings { Username = "tester", BearerToken = "token" }));

		return new BggTestCollectionReader(factory.Object, accounts.Object, _time);
	}

	private sealed class StubHandler(BggTestCollectionReaderTests owner) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			owner._calls++;
			return Task.FromResult(owner._responses.Dequeue()());
		}
	}
}