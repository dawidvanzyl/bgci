using GameCollection.Application.Commands;
using GameCollection.Domain.Aggregates;
using GameCollection.Testing;
using GameCollection.Testing.Factories;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace GameCollection.Application.Tests.Commands;

public sealed class AddGameManuallyCommandHandlerTests : IClassFixture<GameCollectionTestFixture>
{
	private readonly GameCollectionTestContext _context;
	private readonly AddGameManuallyCommandHandler _handler;

	public AddGameManuallyCommandHandlerTests(GameCollectionTestFixture fixture)
	{
		_context = fixture.CreateContext();
		_handler = _context.ServiceProvider.GetRequiredService<AddGameManuallyCommandHandler>();
	}

	[Fact]
	public async Task Handle_AddsGameToRepositoryAndReturnsItsId()
	{
		CollectedGame? added = null;
		_context.Repositories.CollectedGameRepositoryMock
			.Setup(r => r.AddAsync(It.IsAny<CollectedGame>(), It.IsAny<CancellationToken>()))
			.Callback<CollectedGame, CancellationToken>((game, _) => added = game)
			.Returns(Task.CompletedTask);

		var id = await _handler.Handle(AddGameManuallyCommandFactory.Create(name: "Catan"), CancellationToken.None);

		added.ShouldNotBeNull();
		added.Id.Value.ShouldBe(id);
		added.Name.Value.ShouldBe("Catan");
		added.PlayerCount.ShouldNotBeNull();
	}

	[Fact]
	public async Task Handle_EmptyName_ThrowsAndStoresNothing()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _handler.Handle(AddGameManuallyCommandFactory.Create(name: " "), CancellationToken.None));

		_context.Repositories.CollectedGameRepositoryMock
			.Verify(r => r.AddAsync(It.IsAny<CollectedGame>(), It.IsAny<CancellationToken>()), Times.Never);
	}
}