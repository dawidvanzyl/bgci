using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure.Http;
using GameCollection.Application.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace BggIntegration.Infrastructure.Tests;

public sealed class BggCollectionWriterAdapterTests
{
	private readonly Mock<IBggWriterClient> _client = new();

	[Fact]
	[Trait("AC", "90UC1")]
	public async Task AddToCollectionAsync_ClientThrows_ReturnsFailed()
	{
		_client.Setup(c => c.AddToCollectionAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new HttpRequestException("BGG down"));

		var result = await CreateAdapter().AddToCollectionAsync(13, "Wingspan", CancellationToken.None);

		result.ShouldBe(new BggAddResult(BggWriteOutcome.Failed, null));
	}

	[Fact]
	[Trait("AC", "90UC1")]
	public async Task AddToCollectionAsync_ClientTimesOut_ReturnsFailed()
	{
		_client.Setup(c => c.AddToCollectionAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new TaskCanceledException("timeout"));

		var result = await CreateAdapter().AddToCollectionAsync(13, "Wingspan", CancellationToken.None);

		result.ShouldBe(new BggAddResult(BggWriteOutcome.Failed, null));
	}

	[Fact]
	[Trait("AC", "90UC1")]
	public async Task AddToCollectionAsync_NoCollId_ReturnsFailed()
	{
		_client.Setup(c => c.AddToCollectionAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(0);

		var result = await CreateAdapter().AddToCollectionAsync(13, "Wingspan", CancellationToken.None);

		result.ShouldBe(new BggAddResult(BggWriteOutcome.Failed, null));
	}

	[Fact]
	[Trait("AC", "90UC2")]
	public async Task AddToCollectionAsync_ClientReturnsCollId_ReturnsSucceeded()
	{
		_client.Setup(c => c.AddToCollectionAsync("user", 13, It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(555);

		var result = await CreateAdapter().AddToCollectionAsync(13, "Wingspan", CancellationToken.None);

		result.ShouldBe(new BggAddResult(BggWriteOutcome.Succeeded, 555));
	}

	[Theory]
	[Trait("AC", "90UC3")]
	[InlineData("", "http://writer")]
	[InlineData("user", "")]
	public async Task AddToCollectionAsync_NotConfigured_ReturnsSkipped(string username, string baseUrl)
	{
		var result = await CreateAdapter(username, baseUrl).AddToCollectionAsync(13, "Wingspan", CancellationToken.None);

		result.ShouldBe(new BggAddResult(BggWriteOutcome.Skipped, null));
		_client.VerifyNoOtherCalls();
	}

	[Theory]
	[Trait("AC", "90UC3")]
	[InlineData("", "http://writer")]
	[InlineData("user", "")]
	public async Task RemoveFromCollectionAsync_NotConfigured_ReturnsSkipped(string username, string baseUrl)
	{
		var result = await CreateAdapter(username, baseUrl).RemoveFromCollectionAsync(99, CancellationToken.None);

		result.ShouldBe(BggWriteOutcome.Skipped);
		_client.VerifyNoOtherCalls();
	}

	[Fact]
	[Trait("AC", "90UC4")]
	public async Task RemoveFromCollectionAsync_ClientThrows_ReturnsFailed()
	{
		_client.Setup(c => c.RemoveFromCollectionAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new HttpRequestException("BGG down"));

		var result = await CreateAdapter().RemoveFromCollectionAsync(99, CancellationToken.None);

		result.ShouldBe(BggWriteOutcome.Failed);
	}

	[Fact]
	[Trait("AC", "90UC4")]
	public async Task RemoveFromCollectionAsync_ClientTimesOut_ReturnsFailed()
	{
		_client.Setup(c => c.RemoveFromCollectionAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new TaskCanceledException("timeout"));

		var result = await CreateAdapter().RemoveFromCollectionAsync(99, CancellationToken.None);

		result.ShouldBe(BggWriteOutcome.Failed);
	}

	[Fact]
	public async Task RemoveFromCollectionAsync_ClientReturns_ReturnsSucceeded()
	{
		var result = await CreateAdapter().RemoveFromCollectionAsync(99, CancellationToken.None);

		result.ShouldBe(BggWriteOutcome.Succeeded);
		_client.Verify(c => c.RemoveFromCollectionAsync("user", 99, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Theory]
	[Trait("AC", "90UC5")]
	[InlineData(null)]
	[InlineData(0L)]
	public async Task RemoveFromCollectionAsync_NoCollId_ReturnsFailedWithoutCallingClient(long? collId)
	{
		var result = await CreateAdapter().RemoveFromCollectionAsync(collId, CancellationToken.None);

		result.ShouldBe(BggWriteOutcome.Failed);
		_client.VerifyNoOtherCalls();
	}

	[Fact]
	public async Task AddToCollectionAsync_RequestCancelled_Throws()
	{
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();
		_client.Setup(c => c.AddToCollectionAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new OperationCanceledException(cts.Token));

		await Should.ThrowAsync<OperationCanceledException>(
			() => CreateAdapter().AddToCollectionAsync(13, "Wingspan", cts.Token));
	}

	[Fact]
	public async Task RemoveFromCollectionAsync_RequestCancelled_Throws()
	{
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();
		_client.Setup(c => c.RemoveFromCollectionAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new OperationCanceledException(cts.Token));

		await Should.ThrowAsync<OperationCanceledException>(
			() => CreateAdapter().RemoveFromCollectionAsync(99, cts.Token));
	}

	private BggCollectionWriterAdapter CreateAdapter(string username = "user", string baseUrl = "http://writer")
	{
		return new BggCollectionWriterAdapter(
			_client.Object,
			Options.Create(new BggSettings { Username = username }),
			Options.Create(new BggWriterSettings { BaseUrl = baseUrl }),
			NullLogger<BggCollectionWriterAdapter>.Instance);
	}
}