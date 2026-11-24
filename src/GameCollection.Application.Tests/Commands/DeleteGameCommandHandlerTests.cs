using GameCollection.Application.Commands;
using GameCollection.Domain.ValueObjects;
using GameCollection.Testing;
using GameCollection.Testing.Factories;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
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
	[Trait("AC", "111UC2")]
	public async Task Handle_BggGame_DeletesItAndQueuesTheRemoveWithTheCollectionItemId()
	{
		var game = CollectedGameFactory.CreateFromBgg(bggId: 230802, bggCollId: 777);
		_context.Repositories.HasStored(game);

		await _handler.Handle(new DeleteGameCommand(game.Id.Value), CancellationToken.None);

		VerifyDeleted(game.Id);
		_context.Services.BggWriteQueueMock
			.Verify(q => q.QueueRemoveAsync(game.Id.Value, 230802, 777, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	[Trait("AC", "111UC2")]
	public async Task Handle_BggGameWithoutCollId_QueuesTheRemoveWithNull()
	{
		var game = CollectedGameFactory.CreateFromBgg(bggCollId: null);
		_context.Repositories.HasStored(game);

		await _handler.Handle(new DeleteGameCommand(game.Id.Value), CancellationToken.None);

		_context.Services.BggWriteQueueMock
			.Verify(q => q.QueueRemoveAsync(game.Id.Value, CollectedGameFactory.DefaultBggId, null, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	[Trait("AC", "111UC3")]
	public async Task Handle_ManualGame_DeletesItAndNeverQueues()
	{
		var game = CollectedGameFactory.CreateManual();
		_context.Repositories.HasStored(game);

		await _handler.Handle(new DeleteGameCommand(game.Id.Value), CancellationToken.None);

		VerifyDeleted(game.Id);
		_context.Services.BggWriteQueueMock
			.Verify(q => q.QueueRemoveAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	[Trait("AC", "114UC3")]
	public async Task Handle_BggGameWithSkipBggWrite_DeletesItAndNeverQueues()
	{
		var game = CollectedGameFactory.CreateFromBgg();
		_context.Repositories.HasStored(game);

		await _handler.Handle(new DeleteGameCommand(game.Id.Value, SkipBggWrite: true), CancellationToken.None);

		VerifyDeleted(game.Id);
		_context.Services.BggWriteQueueMock
			.Verify(q => q.QueueRemoveAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Handle_QueueFails_GameStaysDeletedAndTheExceptionPropagates()
	{
		var game = CollectedGameFactory.CreateFromBgg();
		_context.Repositories.HasStored(game);
		_context.Services.QueueRemoveThrows(new InvalidOperationException("queue down"));

		await Should.ThrowAsync<InvalidOperationException>(
			() => _handler.Handle(new DeleteGameCommand(game.Id.Value), CancellationToken.None));

		VerifyDeleted(game.Id);
	}

	[Fact]
	public async Task Handle_UnknownGame_ThrowsKeyNotFoundAndDeletesNothing()
	{
		await Should.ThrowAsync<KeyNotFoundException>(
			() => _handler.Handle(new DeleteGameCommand(Guid.NewGuid()), CancellationToken.None));

		_context.Repositories.CollectedGameRepositoryMock
			.Verify(r => r.DeleteAsync(It.IsAny<GameId>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	private void VerifyDeleted(GameId id)
	{
		_context.Repositories.CollectedGameRepositoryMock
			.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
	}
}