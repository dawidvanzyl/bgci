using GameCollection.Application.Abstractions;
using GameCollection.Application.Commands;
using GameCollection.Domain.Aggregates;
using GameCollection.Domain.Repositories;
using GameCollection.Testing;
using GameCollection.Testing.Factories;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace GameCollection.Application.Tests.Commands;

public sealed class AddGameFromBggCommandHandlerTests : IClassFixture<GameCollectionTestFixture>
{
	private readonly GameCollectionTestContext _context;
	private readonly AddGameFromBggCommandHandler _handler;

	public AddGameFromBggCommandHandlerTests(GameCollectionTestFixture fixture)
	{
		_context = fixture.CreateContext();
		_handler = _context.ServiceProvider.GetRequiredService<AddGameFromBggCommandHandler>();
	}

	[Fact]
	[Trait("AC", "90UC1")]
	public async Task Handle_BggAddFails_SavesWithoutCollIdAndReportsFailed()
	{
		_context.Services.BggAddReturns(new BggAddResult(BggWriteOutcome.Failed, null));

		var result = await _handler.Handle(AddGameFromBggCommandFactory.Create(), CancellationToken.None);

		result.BggWrite.ShouldBe(BggWriteOutcome.Failed);
		SavedGame().BggCollId.ShouldBeNull();
	}

	[Fact]
	[Trait("AC", "90UC1")]
	public async Task Handle_BggAddThrows_SavesWithoutCollIdAndReportsFailed()
	{
		_context.Services.BggAddThrows(new HttpRequestException("BGG down"));

		var result = await _handler.Handle(AddGameFromBggCommandFactory.Create(), CancellationToken.None);

		result.BggWrite.ShouldBe(BggWriteOutcome.Failed);
		SavedGame().BggCollId.ShouldBeNull();
	}

	[Fact]
	[Trait("AC", "90UC1")]
	public async Task Handle_BggAddTimesOut_SavesWithoutCollIdAndReportsFailed()
	{
		_context.Services.BggAddThrows(new TaskCanceledException("timeout"));

		var result = await _handler.Handle(AddGameFromBggCommandFactory.Create(), CancellationToken.None);

		result.BggWrite.ShouldBe(BggWriteOutcome.Failed);
		SavedGame().BggCollId.ShouldBeNull();
	}

	[Fact]
	[Trait("AC", "90UC2")]
	public async Task Handle_BggAddSucceeds_SavesCollIdAndReportsSucceeded()
	{
		_context.Services.BggAddReturns(new BggAddResult(BggWriteOutcome.Succeeded, 555));

		var result = await _handler.Handle(AddGameFromBggCommandFactory.Create(), CancellationToken.None);

		result.BggWrite.ShouldBe(BggWriteOutcome.Succeeded);
		SavedGame().BggCollId.ShouldBe(555);
	}

	[Fact]
	[Trait("AC", "90UC3")]
	public async Task Handle_BggAddSkipped_ReportsSkipped()
	{
		_context.Services.BggAddReturns(new BggAddResult(BggWriteOutcome.Skipped, null));

		var result = await _handler.Handle(AddGameFromBggCommandFactory.Create(), CancellationToken.None);

		result.BggWrite.ShouldBe(BggWriteOutcome.Skipped);
		SavedGame().BggCollId.ShouldBeNull();
	}

	[Fact]
	public async Task Handle_SkipBggWrite_ReportsNoneWithoutCallingBgg()
	{
		var command = AddGameFromBggCommandFactory.Create(skipBggWrite: true, bggCollId: 42);

		var result = await _handler.Handle(command, CancellationToken.None);

		result.BggWrite.ShouldBe(BggWriteOutcome.None);
		SavedGame().BggCollId.ShouldBe(42);
		_context.Services.BggCollectionWriterMock
			.Verify(w => w.AddToCollectionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Handle_RequestCancelled_Propagates()
	{
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();
		_context.Services.BggAddThrows(new OperationCanceledException(cts.Token));

		await Should.ThrowAsync<OperationCanceledException>(
			() => _handler.Handle(AddGameFromBggCommandFactory.Create(), cts.Token));
	}

	private CollectedGame SavedGame()
	{
		return _context.Repositories.CollectedGameRepositoryMock.Invocations
			.Where(i => i.Method.Name == nameof(ICollectedGameRepository.AddAsync))
			.Select(i => (CollectedGame)i.Arguments[0])
			.Single();
	}
}