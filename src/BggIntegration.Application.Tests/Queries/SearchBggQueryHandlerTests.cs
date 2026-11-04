using BggIntegration.Application.Queries;
using BggIntegration.Testing;
using BggIntegration.Testing.Factories;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace BggIntegration.Application.Tests.Queries;

public sealed class SearchBggQueryHandlerTests : IClassFixture<BggIntegrationTestFixture>
{
	private readonly BggIntegrationTestContext _context;
	private readonly SearchBggQueryHandler _handler;

	public SearchBggQueryHandlerTests(BggIntegrationTestFixture fixture)
	{
		_context = fixture.CreateContext();
		_handler = _context.ServiceProvider.GetRequiredService<SearchBggQueryHandler>();
	}

	[Fact]
	public async Task Handle_Query_ReturnsClientResults()
	{
		var expected = new[] { BggSearchResultFactory.Create() };
		_context.Clients.BggClientMock
			.Setup(c => c.SearchAsync("catan", It.IsAny<CancellationToken>()))
			.ReturnsAsync(expected);

		var results = await _handler.Handle(new SearchBggQuery("catan"), CancellationToken.None);

		results.ShouldBe(expected);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public async Task Handle_BlankQuery_ReturnsEmptyWithoutCallingBgg(string query)
	{
		var results = await _handler.Handle(new SearchBggQuery(query), CancellationToken.None);

		results.ShouldBeEmpty();
		_context.Clients.BggClientMock
			.Verify(c => c.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
	}
}