using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure.Http;
using Jobs.Worker.Bgg;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Shouldly;
using Xunit;

namespace Jobs.Worker.Tests.Bgg;

public sealed class BggCollectionWriterAdapterTests
{
	private readonly Mock<IBggWriteClient> _client = new();
	private readonly FakeTimeProvider _time = new();

	[Fact]
	public async Task AddAsync_ClientReturnsCollId_ReturnsIt()
	{
		_client.Setup(c => c.AddToCollectionAsync(13, "Wingspan", It.IsAny<CancellationToken>())).ReturnsAsync(555);

		var collId = await CreateAdapter().AddAsync("owner", 13, "Wingspan", CancellationToken.None);

		collId.ShouldBe(555);
	}

	[Fact]
	public async Task AddAsync_ClientThrows_PropagatesTheException()
	{
		_client.Setup(c => c.AddToCollectionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new BggWriteException("add", "BGG add failed with status 500."));

		var exception = await Should.ThrowAsync<BggWriteException>(() => CreateAdapter().AddAsync("owner", 13, "Wingspan", CancellationToken.None));

		exception.Step.ShouldBe("add");
	}

	[Fact]
	public async Task AddAsync_NoCollId_Throws()
	{
		_client.Setup(c => c.AddToCollectionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

		await Should.ThrowAsync<BggWriteException>(() => CreateAdapter().AddAsync("owner", 13, "Wingspan", CancellationToken.None));
	}

	[Fact]
	public async Task AddAsync_ExceedsTheTimeLimit_ThrowsAWriteException()
	{
		_client.Setup(c => c.AddToCollectionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.Returns(async (int _, string _, CancellationToken token) =>
			{
				await Task.Delay(Timeout.Infinite, token);
				return 0L;
			});

		var add = CreateAdapter().AddAsync("owner", 13, "Wingspan", CancellationToken.None);
		add.IsCompleted.ShouldBeFalse();
		_time.Advance(TimeSpan.FromSeconds(60));

		var exception = await Should.ThrowAsync<BggWriteException>(() => add);
		exception.Step.ShouldBe("add");
	}

	[Fact]
	public async Task AddAsync_CallerCancels_ThrowsOperationCanceled()
	{
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();
		_client.Setup(c => c.AddToCollectionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new OperationCanceledException(cts.Token));

		await Should.ThrowAsync<OperationCanceledException>(() => CreateAdapter().AddAsync("owner", 13, "Wingspan", cts.Token));
	}

	[Theory]
	[InlineData("", "secret", "token")]
	[InlineData("user", "", "token")]
	[InlineData("user", "secret", "")]
	public async Task AddAsync_NotConfigured_ThrowsWithoutCallingBgg(string username, string password, string bearerToken)
	{
		await Should.ThrowAsync<BggWriteException>(
			() => CreateAdapter(username, password, bearerToken).AddAsync("owner", 13, "Wingspan", CancellationToken.None));

		_client.VerifyNoOtherCalls();
	}

	[Fact]
	public async Task RemoveAsync_ClientReturns_RemovesTheItem()
	{
		await CreateAdapter().RemoveAsync("owner", 99, CancellationToken.None);

		_client.Verify(c => c.RemoveFromCollectionAsync(99, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task RemoveAsync_ClientThrows_PropagatesTheException()
	{
		_client.Setup(c => c.RemoveFromCollectionAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new BggWriteException("remove", "BGG remove failed with status 500."));

		await Should.ThrowAsync<BggWriteException>(() => CreateAdapter().RemoveAsync("owner", 99, CancellationToken.None));
	}

	[Fact]
	public async Task RemoveAsync_ExceedsTheTimeLimit_ThrowsAWriteException()
	{
		_client.Setup(c => c.RemoveFromCollectionAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
			.Returns((long _, CancellationToken token) => Task.Delay(Timeout.Infinite, token));

		var remove = CreateAdapter().RemoveAsync("owner", 99, CancellationToken.None);
		remove.IsCompleted.ShouldBeFalse();
		_time.Advance(TimeSpan.FromSeconds(60));

		var exception = await Should.ThrowAsync<BggWriteException>(() => remove);
		exception.Step.ShouldBe("remove");
	}

	[Theory]
	[InlineData("", "secret")]
	[InlineData("user", "")]
	public async Task RemoveAsync_NotConfigured_ThrowsWithoutCallingBgg(string username, string password)
	{
		await Should.ThrowAsync<BggWriteException>(
			() => CreateAdapter(username, password).RemoveAsync("owner", 99, CancellationToken.None));

		_client.VerifyNoOtherCalls();
	}

	[Fact]
	[Trait("AC", "111UC7")]
	public async Task FindCollectionItemIdAsync_ClientFindsTheItem_ReturnsItsId()
	{
		_client.Setup(c => c.FindCollectionItemIdAsync(13, It.IsAny<CancellationToken>())).ReturnsAsync(555);

		var collId = await CreateAdapter().FindCollectionItemIdAsync("owner", 13, CancellationToken.None);

		collId.ShouldBe(555);
	}

	[Fact]
	[Trait("AC", "111UC7")]
	public async Task FindCollectionItemIdAsync_ClientFindsNothing_ReturnsNull()
	{
		_client.Setup(c => c.FindCollectionItemIdAsync(13, It.IsAny<CancellationToken>())).ReturnsAsync((long?)null);

		(await CreateAdapter().FindCollectionItemIdAsync("owner", 13, CancellationToken.None)).ShouldBeNull();
	}

	[Theory]
	[InlineData("", "token")]
	[InlineData("user", "")]
	public async Task FindCollectionItemIdAsync_NotConfigured_ThrowsWithoutCallingBgg(string username, string bearerToken)
	{
		await Should.ThrowAsync<BggWriteException>(
			() => CreateAdapter(username, "secret", bearerToken).FindCollectionItemIdAsync("owner", 13, CancellationToken.None));

		_client.VerifyNoOtherCalls();
	}

	private BggCollectionWriterAdapter CreateAdapter(string username = "user", string password = "secret", string bearerToken = "token")
	{
		var accounts = new Mock<IBggAccountClients>();
		accounts
			.Setup(a => a.Get("owner"))
			.Returns(new BggAccountClient(
				_client.Object,
				new BggSettings { Username = username, Password = password, BearerToken = bearerToken }));
		return new BggCollectionWriterAdapter(accounts.Object, _time);
	}
}