using GameCollection.Application.Commands;
using GameCollection.Domain.Aggregates;
using GameCollection.Domain.ValueObjects;
using GameCollection.Testing;
using GameCollection.Testing.Factories;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace GameCollection.Application.Tests.Commands;

public sealed class MarkGameBggFailedCommandHandlerTests : IClassFixture<GameCollectionTestFixture>
{
	private readonly GameCollectionTestContext _context;
	private readonly MarkGameBggFailedCommandHandler _handler;

	public MarkGameBggFailedCommandHandlerTests(GameCollectionTestFixture fixture)
	{
		_context = fixture.CreateContext();
		_handler = _context.ServiceProvider.GetRequiredService<MarkGameBggFailedCommandHandler>();
	}

	[Fact]
	[Trait("AC", "111UC8")]
	public async Task Handle_PendingGame_StoresFailedAndKeepsTheCollectionItemId()
	{
		var game = CollectedGameFactory.CreateFromBgg(bggCollId: 12, bggStatus: BggStatus.Pending);
		_context.Repositories.HasStored(game);

		var result = await _handler.Handle(new MarkGameBggFailedCommand(game.Id.Value), CancellationToken.None);

		result.ShouldBeTrue();
		_context.Repositories.CollectedGameRepositoryMock
			.Verify(r => r.UpdateBggStatusAsync(
				It.Is<CollectedGame>(g => g.BggStatus == BggStatus.Failed && g.BggCollId == 12),
				It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	[Trait("AC", "111UC8")]
	public async Task Handle_UnknownGame_ReturnsFalseAndStoresNothing()
	{
		var result = await _handler.Handle(new MarkGameBggFailedCommand(Guid.NewGuid()), CancellationToken.None);

		result.ShouldBeFalse();
		_context.Repositories.CollectedGameRepositoryMock
			.Verify(r => r.UpdateBggStatusAsync(It.IsAny<CollectedGame>(), It.IsAny<CancellationToken>()), Times.Never);
	}
}