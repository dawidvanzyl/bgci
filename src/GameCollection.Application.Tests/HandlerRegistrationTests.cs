using GameCollection.Application.Commands;
using GameCollection.Testing;
using Shared.Testing;
using Xunit;

namespace GameCollection.Application.Tests;

public sealed class HandlerRegistrationTests : IClassFixture<GameCollectionTestFixture>
{
	private readonly GameCollectionTestContext _context;

	public HandlerRegistrationTests(GameCollectionTestFixture fixture)
	{
		_context = fixture.CreateContext();
	}

	[Fact]
	public void EveryHandlerInTheApplicationAssembly_IsRegisteredInTheTestFixture()
	{
		HandlerRegistrationAssertions.AssertEveryHandlerIsRegistered(
			typeof(AddGameManuallyCommand).Assembly, _context.ServiceProvider, nameof(GameCollectionTestFixture));
	}
}