using GameCollection.Application.Commands;
using GameCollection.Testing;
using GameCollection.Testing.Factories;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace GameCollection.Application.Tests.Commands;

public sealed class DeleteGameCommandHandlerTests : IClassFixture<GameCollectionTestFixture>
{
	private readonly GameCollectionTestContext _context;
	private readonly DeleteGameCommandHandler _handler;

	public DeleteGameCommandHandlerTests(GameCollectionTestFixture fixture)
	{
		_context = fixture.CreateContext();
		_handler = _context.ServiceProvider.GetRequiredService<DeleteGameCommandHandler>();
	}

	[Fact]
	public async Task Handle_ManualGame_DeletesWithoutCallingBgg()
	{
		var game = CollectedGameFactory.CreateManual();
		_context.Repositories.HasStored(game);

		await _handler.Handle(new DeleteGameCommand(game.Id.Value), CancellationToken.None);

		_context.Services.BggCollectionWriterMock
			.Verify(w => w.RemoveFromCollectionAsync(It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
		VerifyDeleted(game.Id);
	}

	[Fact]
	public async Task Handle_BggGame_RemovesFromBggThenDeletes()
	{
		var game = CollectedGameFactory.CreateFromBgg();
		_context.Repositories.HasStored(game);

		await _handler.Handle(new DeleteGameCommand(game.Id.Value), CancellationToken.None);

		_context.Services.BggCollectionWriterMock
			.Verify(w => w.RemoveFromCollectionAsync(CollectedGameFactory.DefaultBggCollId, It.IsAny<CancellationToken>()), Times.Once);
		VerifyDeleted(game.Id);
	}

	[Fact]
	public async Task Handle_BggWriteFails_StillDeletesLocally()
	{
		var game = CollectedGameFactory.CreateFromBgg();
		_context.Repositories.HasStored(game);
		_context.Services.BggCollectionWriterMock
			.Setup(w => w.RemoveFromCollectionAsync(It.IsAny<long?>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new HttpRequestException("BGG down"));

		await _handler.Handle(new DeleteGameCommand(game.Id.Value), CancellationToken.None);

		VerifyDeleted(game.Id);
	}

	private void VerifyDeleted(GameCollection.Domain.ValueObjects.GameId id)
	{
		_context.Repositories.CollectedGameRepositoryMock
			.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
	}
}