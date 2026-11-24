using GameCollection.Application.Queries;
using GameCollection.Domain.Aggregates;
using GameCollection.Domain.Repositories;
using GameCollection.Domain.ValueObjects;
using GameCollection.Testing;
using GameCollection.Testing.Factories;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace GameCollection.Application.Tests.Queries;

public sealed class GetAllGamesQueryHandlerTests : IClassFixture<GameCollectionTestFixture>
{
	private readonly GameCollectionTestContext _context;
	private readonly GetAllGamesQueryHandler _handler;

	public GetAllGamesQueryHandlerTests(GameCollectionTestFixture fixture)
	{
		_context = fixture.CreateContext();
		_handler = _context.ServiceProvider.GetRequiredService<GetAllGamesQueryHandler>();
	}

	[Fact]
	[Trait("AC", "111UC15")]
	public async Task Handle_GamesWithEveryStatus_ReturnsLowerCaseStatusAndNullForManualGames()
	{
		var pending = CollectedGameFactory.CreateFromBgg("Pending game", bggId: 1, bggCollId: null, bggStatus: BggStatus.Pending);
		var sent = CollectedGameFactory.CreateFromBgg("Sent game", bggId: 2);
		var failed = CollectedGameFactory.CreateFromBgg("Failed game", bggId: 3, bggCollId: null, bggStatus: BggStatus.Failed);
		var manual = CollectedGameFactory.CreateManual("Manual game");
		_context.Repositories.CollectedGameRepositoryMock
			.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(new List<CollectedGame> { pending, sent, failed, manual });

		var result = await _handler.Handle(new GetAllGamesQuery(), CancellationToken.None);

		result.Select(g => g.BggStatus).ToArray().ShouldBe(["pending", "sent", "failed", null]);
	}
}