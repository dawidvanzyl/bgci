using GameCollection.Application.Queries;
using GameCollection.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace GameCollection.Application.Tests.Queries;

public sealed class GetPendingBggRemovesQueryHandlerTests : IClassFixture<GameCollectionTestFixture>
{
	private readonly GameCollectionTestContext _context;
	private readonly GetPendingBggRemovesQueryHandler _handler;

	public GetPendingBggRemovesQueryHandlerTests(GameCollectionTestFixture fixture)
	{
		_context = fixture.CreateContext();
		_handler = _context.ServiceProvider.GetRequiredService<GetPendingBggRemovesQueryHandler>();
	}

	[Fact]
	[Trait("AC", "114UC2")]
	public async Task Handle_Query_ReturnsTheBggIdsThatTheQueueReports()
	{
		_context.Services.BggWriteQueueMock
			.Setup(q => q.GetPendingRemoveBggIdsAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync([230802, 13]);

		var ids = await _handler.Handle(new GetPendingBggRemovesQuery(), CancellationToken.None);

		ids.ShouldBe([230802, 13]);
	}
}