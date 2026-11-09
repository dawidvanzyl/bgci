using GameCollection.Application.Abstractions;
using GameCollection.Application.Commands;
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
	[Trait("AC", "90UC6")]
	public async Task Handle_ManualGame_DeletesWithoutCallingBggAndReportsNone()
	{
		var game = CollectedGameFactory.CreateManual();
		_context.Repositories.HasStored(game);

		var result = await _handler.Handle(new DeleteGameCommand(game.Id.Value), CancellationToken.None);

		result.BggWrite.ShouldBe(BggWriteOutcome.None);
		_context.Services.BggCollectionWriterMock
			.Verify(w => w.RemoveFromCollectionAsync(It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
		VerifyDeleted(game.Id);
	}

	[Fact]
	public async Task Handle_BggGame_RemovesFromBggThenDeletes()
	{
		var game = CollectedGameFactory.CreateFromBgg();
		_context.Repositories.HasStored(game);
		_context.Services.BggRemoveReturns(BggWriteOutcome.Succeeded);

		var result = await _handler.Handle(new DeleteGameCommand(game.Id.Value), CancellationToken.None);

		result.BggWrite.ShouldBe(BggWriteOutcome.Succeeded);
		_context.Services.BggCollectionWriterMock
			.Verify(w => w.RemoveFromCollectionAsync(CollectedGameFactory.DefaultBggCollId, It.IsAny<CancellationToken>()), Times.Once);
		VerifyDeleted(game.Id);
	}

	[Fact]
	[Trait("AC", "90UC3")]
	public async Task Handle_BggRemoveSkipped_ReportsSkipped()
	{
		var game = CollectedGameFactory.CreateFromBgg();
		_context.Repositories.HasStored(game);
		_context.Services.BggRemoveReturns(BggWriteOutcome.Skipped);

		var result = await _handler.Handle(new DeleteGameCommand(game.Id.Value), CancellationToken.None);

		result.BggWrite.ShouldBe(BggWriteOutcome.Skipped);
		VerifyDeleted(game.Id);
	}

	[Fact]
	[Trait("AC", "90UC4")]
	public async Task Handle_BggRemoveFails_DeletesAndReportsFailed()
	{
		var game = CollectedGameFactory.CreateFromBgg();
		_context.Repositories.HasStored(game);
		_context.Services.BggRemoveReturns(BggWriteOutcome.Failed);

		var result = await _handler.Handle(new DeleteGameCommand(game.Id.Value), CancellationToken.None);

		result.BggWrite.ShouldBe(BggWriteOutcome.Failed);
		VerifyDeleted(game.Id);
	}

	[Fact]
	[Trait("AC", "90UC4")]
	public async Task Handle_BggRemoveThrows_DeletesAndReportsFailed()
	{
		var game = CollectedGameFactory.CreateFromBgg();
		_context.Repositories.HasStored(game);
		_context.Services.BggRemoveThrows(new HttpRequestException("BGG down"));

		var result = await _handler.Handle(new DeleteGameCommand(game.Id.Value), CancellationToken.None);

		result.BggWrite.ShouldBe(BggWriteOutcome.Failed);
		VerifyDeleted(game.Id);
	}

	[Fact]
	[Trait("AC", "90UC4")]
	public async Task Handle_BggRemoveTimesOut_DeletesAndReportsFailed()
	{
		var game = CollectedGameFactory.CreateFromBgg();
		_context.Repositories.HasStored(game);
		_context.Services.BggRemoveThrows(new TaskCanceledException("timeout"));

		var result = await _handler.Handle(new DeleteGameCommand(game.Id.Value), CancellationToken.None);

		result.BggWrite.ShouldBe(BggWriteOutcome.Failed);
		VerifyDeleted(game.Id);
	}

	[Fact]
	[Trait("AC", "90UC5")]
	public async Task Handle_BggGameWithoutCollId_DeletesAndReportsFailed()
	{
		var game = CollectedGameFactory.CreateFromBgg(bggCollId: null);
		_context.Repositories.HasStored(game);
		_context.Services.BggRemoveReturns(BggWriteOutcome.Failed);

		var result = await _handler.Handle(new DeleteGameCommand(game.Id.Value), CancellationToken.None);

		result.BggWrite.ShouldBe(BggWriteOutcome.Failed);
		VerifyDeleted(game.Id);
	}

	[Fact]
	public async Task Handle_RequestCancelled_Propagates()
	{
		var game = CollectedGameFactory.CreateFromBgg();
		_context.Repositories.HasStored(game);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();
		_context.Services.BggRemoveThrows(new OperationCanceledException(cts.Token));

		await Should.ThrowAsync<OperationCanceledException>(
			() => _handler.Handle(new DeleteGameCommand(game.Id.Value), cts.Token));
	}

	private void VerifyDeleted(GameCollection.Domain.ValueObjects.GameId id)
	{
		_context.Repositories.CollectedGameRepositoryMock
			.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
	}
}