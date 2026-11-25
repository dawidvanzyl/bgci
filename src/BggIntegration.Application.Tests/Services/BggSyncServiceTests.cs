using BggIntegration.Application.Services;
using BggIntegration.Domain.Models;
using BggIntegration.Testing;
using BggIntegration.Testing.Factories;
using GameCollection.Application.Commands;
using GameCollection.Application.DTOs;
using GameCollection.Application.Queries;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace BggIntegration.Application.Tests.Services;

public sealed class BggSyncServiceTests : IClassFixture<BggIntegrationTestFixture>
{
	private const string _username = "owner";

	private readonly BggIntegrationTestContext _context;
	private readonly BggSyncService _service;

	public BggSyncServiceTests(BggIntegrationTestFixture fixture)
	{
		_context = fixture.CreateContext();
		_service = _context.ServiceProvider.GetRequiredService<BggSyncService>();
		_context.Services.HasLocalGames();
		_context.Services.HasPendingRemoves();
		HasBggCollection();
		HasBggExpansionCollection();
	}

	[Theory]
	[InlineData(BggStatusNames.Pending)]
	[InlineData(BggStatusNames.Failed)]
	[Trait("AC", "114UC1")]
	public async Task SyncAsync_GameNotSentAndMissingFromBgg_IsNotDeletedWhileASentGameIs(string status)
	{
		var unsent = CollectedGameDtoFactory.Create(bggId: 101, bggStatus: status);
		var sent = CollectedGameDtoFactory.Create(bggId: 102, bggStatus: BggStatusNames.Sent);
		_context.Services.HasLocalGames(unsent, sent);

		await _service.SyncAsync(_username, CancellationToken.None);

		VerifyDeleted(sent.Id, Times.Once());
		VerifyDeleted(unsent.Id, Times.Never());
	}

	[Fact]
	[Trait("AC", "114UC1")]
	public async Task SyncAsync_Always_ReadsTheLocalGamesBeforeTheBggCollection()
	{
		var calls = RecordCalls();

		await _service.SyncAsync(_username, CancellationToken.None);

		Assert.True(calls.IndexOf("local games") < calls.IndexOf("bgg collection"));
	}

	[Fact]
	[Trait("AC", "114UC2")]
	public async Task SyncAsync_PendingRemoveForBaseGameInBgg_IsNotImportedWhileAnotherMissingGameIs()
	{
		HasBggCollection(BggCollectionItemFactory.Create(bggId: 201), BggCollectionItemFactory.Create(bggId: 202));
		HasDetails([201, 202]);
		_context.Services.HasPendingRemoves(201);

		await _service.SyncAsync(_username, CancellationToken.None);

		VerifyAdded(202, Times.Once());
		VerifyAdded(201, Times.Never());
	}

	[Fact]
	[Trait("AC", "114UC2")]
	public async Task SyncAsync_PendingRemoveForExpansionInBgg_IsNotImportedWhileAnotherMissingExpansionIs()
	{
		var parent = CollectedGameDtoFactory.Create(bggId: 300);
		_context.Services.HasLocalGames(parent);
		HasBggCollection(BggCollectionItemFactory.Create(bggId: 300));
		HasBggExpansionCollection(BggCollectionItemFactory.Create(bggId: 301), BggCollectionItemFactory.Create(bggId: 302));
		HasDetails([301, 302], parentBggId: 300);
		_context.Services.HasPendingRemoves(301);

		await _service.SyncAsync(_username, CancellationToken.None);

		VerifyAdded(302, Times.Once());
		VerifyAdded(301, Times.Never());
	}

	[Fact]
	[Trait("AC", "114UC2")]
	public async Task SyncAsync_Always_ReadsThePendingRemovesAfterTheLocalGamesAndBeforeTheBggCollection()
	{
		var calls = RecordCalls();

		await _service.SyncAsync(_username, CancellationToken.None);

		Assert.True(calls.IndexOf("pending removes") > calls.IndexOf("local games"));
		Assert.True(calls.IndexOf("pending removes") < calls.IndexOf("bgg collection"));
	}

	[Fact]
	[Trait("AC", "114UC3")]
	public async Task SyncAsync_SentGameMissingFromBgg_DeletesItWithoutWritingToBgg()
	{
		var sent = CollectedGameDtoFactory.Create(bggId: 401);
		_context.Services.HasLocalGames(sent);

		await _service.SyncAsync(_username, CancellationToken.None);

		_context.Services.MediatorMock.Verify(
			m => m.Send(It.Is<DeleteGameCommand>(c => c.Id == sent.Id && c.SkipBggWrite), It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	[Trait("AC", "114UC4")]
	public async Task SyncAsync_FailedGameNowInBgg_SendsNoCommandForIt()
	{
		var failed = CollectedGameDtoFactory.Create(bggId: 501, bggStatus: BggStatusNames.Failed);
		_context.Services.HasLocalGames(failed);
		HasBggCollection(BggCollectionItemFactory.Create(bggId: 501));

		await _service.SyncAsync(_username, CancellationToken.None);

		var mediator = _context.Services.MediatorMock;
		mediator.Verify(m => m.Send(It.IsAny<DeleteGameCommand>(), It.IsAny<CancellationToken>()), Times.Never);
		mediator.Verify(m => m.Send(It.IsAny<AddGameFromBggCommand>(), It.IsAny<CancellationToken>()), Times.Never);
		mediator.Verify(m => m.Send(It.IsAny<MarkGameBggSentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
		mediator.Verify(m => m.Send(It.IsAny<MarkGameBggFailedCommand>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	private List<string> RecordCalls()
	{
		var calls = new List<string>();
		_context.Services.MediatorMock
			.Setup(m => m.Send(It.IsAny<GetAllGamesQuery>(), It.IsAny<CancellationToken>()))
			.Callback(() => calls.Add("local games"))
			.ReturnsAsync([]);
		_context.Services.MediatorMock
			.Setup(m => m.Send(It.IsAny<GetPendingBggRemovesQuery>(), It.IsAny<CancellationToken>()))
			.Callback(() => calls.Add("pending removes"))
			.ReturnsAsync([]);
		_context.Clients.BggClientMock
			.Setup(c => c.GetCollectionAsync(_username, It.IsAny<CancellationToken>()))
			.Callback(() => calls.Add("bgg collection"))
			.ReturnsAsync([]);
		return calls;
	}

	private void HasBggCollection(params BggCollectionItem[] items)
	{
		_context.Clients.BggClientMock
			.Setup(c => c.GetCollectionAsync(_username, It.IsAny<CancellationToken>()))
			.ReturnsAsync(items);
	}

	private void HasBggExpansionCollection(params BggCollectionItem[] items)
	{
		_context.Clients.BggClientMock
			.Setup(c => c.GetExpansionCollectionAsync(_username, It.IsAny<CancellationToken>()))
			.ReturnsAsync(items);
	}

	private void HasDetails(int[] bggIds, int? parentBggId = null)
	{
		foreach (var bggId in bggIds)
		{
			var details = BggGameDetailsFactory.Create(bggId: bggId) with
			{
				ParentBggIds = parentBggId.HasValue ? [parentBggId.Value] : []
			};
			_context.Clients.BggClientMock
				.Setup(c => c.GetGameDetailsAsync(bggId, It.IsAny<CancellationToken>()))
				.ReturnsAsync(details);
		}
	}

	private void VerifyAdded(int bggId, Times times)
	{
		_context.Services.MediatorMock.Verify(
			m => m.Send(It.Is<AddGameFromBggCommand>(c => c.BggId == bggId), It.IsAny<CancellationToken>()), times);
	}

	private void VerifyDeleted(Guid id, Times times)
	{
		_context.Services.MediatorMock.Verify(
			m => m.Send(It.Is<DeleteGameCommand>(c => c.Id == id), It.IsAny<CancellationToken>()), times);
	}
}