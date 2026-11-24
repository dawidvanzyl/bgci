using GameCollection.Application.Commands;
using GameCollection.Domain.Aggregates;
using GameCollection.Domain.Repositories;
using GameCollection.Domain.ValueObjects;
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
	[Trait("AC", "111UC1")]
	public async Task Handle_Add_SavesPendingGameWithoutCollIdAndQueuesTheAddOnce()
	{
		var command = AddGameFromBggCommandFactory.Create(name: "Azul", bggId: 230802);

		var result = await _handler.Handle(command, CancellationToken.None);

		var saved = SavedGame();
		saved.BggStatus.ShouldBe(BggStatus.Pending);
		saved.BggCollId.ShouldBeNull();
		result.Id.ShouldBe(saved.Id.Value);
		result.BggStatus.ShouldBe("pending");
		_context.Services.BggWriteQueueMock
			.Verify(q => q.QueueAddAsync(saved.Id.Value, 230802, "Azul", It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	[Trait("AC", "111UC4")]
	public async Task Handle_QueueFails_DeletesTheNewGameAndRethrows()
	{
		_context.Services.QueueAddThrows(new InvalidOperationException("queue down"));

		await Should.ThrowAsync<InvalidOperationException>(
			() => _handler.Handle(AddGameFromBggCommandFactory.Create(), CancellationToken.None));

		_context.Repositories.CollectedGameRepositoryMock
			.Verify(r => r.DeleteAsync(SavedGame().Id, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	[Trait("AC", "111UC14")]
	public async Task Handle_SkipBggWrite_SavesSentGameWithTheGivenCollIdAndNeverQueues()
	{
		var command = AddGameFromBggCommandFactory.Create(skipBggWrite: true, bggCollId: 42);

		var result = await _handler.Handle(command, CancellationToken.None);

		var saved = SavedGame();
		saved.BggStatus.ShouldBe(BggStatus.Sent);
		saved.BggCollId.ShouldBe(42);
		result.BggStatus.ShouldBe("sent");
		_context.Services.BggWriteQueueMock
			.Verify(q => q.QueueAddAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	private CollectedGame SavedGame()
	{
		return _context.Repositories.CollectedGameRepositoryMock.Invocations
			.Where(i => i.Method.Name == nameof(ICollectedGameRepository.AddAsync))
			.Select(i => (CollectedGame)i.Arguments[0])
			.Single();
	}
}