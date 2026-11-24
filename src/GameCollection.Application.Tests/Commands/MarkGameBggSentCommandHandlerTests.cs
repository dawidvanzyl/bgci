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

public sealed class MarkGameBggSentCommandHandlerTests : IClassFixture<GameCollectionTestFixture>
{
	private readonly GameCollectionTestContext _context;
	private readonly MarkGameBggSentCommandHandler _handler;

	public MarkGameBggSentCommandHandlerTests(GameCollectionTestFixture fixture)
	{
		_context = fixture.CreateContext();
		_handler = _context.ServiceProvider.GetRequiredService<MarkGameBggSentCommandHandler>();
	}

	[Fact]
	[Trait("AC", "111UC6")]
	public async Task Handle_PendingGame_StoresSentAndTheCollectionItemId()
	{
		var game = CollectedGameFactory.CreateFromBgg(bggCollId: null, bggStatus: BggStatus.Pending);
		_context.Repositories.HasStored(game);

		var result = await _handler.Handle(new MarkGameBggSentCommand(game.Id.Value, 555), CancellationToken.None);

		result.ShouldBeTrue();
		_context.Repositories.CollectedGameRepositoryMock
			.Verify(r => r.UpdateBggStatusAsync(
				It.Is<CollectedGame>(g => g.BggStatus == BggStatus.Sent && g.BggCollId == 555),
				It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	[Trait("AC", "111UC6")]
	public async Task Handle_UnknownGame_ReturnsFalseAndStoresNothing()
	{
		var result = await _handler.Handle(new MarkGameBggSentCommand(Guid.NewGuid(), 555), CancellationToken.None);

		result.ShouldBeFalse();
		_context.Repositories.CollectedGameRepositoryMock
			.Verify(r => r.UpdateBggStatusAsync(It.IsAny<CollectedGame>(), It.IsAny<CancellationToken>()), Times.Never);
	}
}
