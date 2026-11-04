using BggIntegration.Application.Queries;
using BggIntegration.Testing;
using Shared.Testing;
using Xunit;

namespace BggIntegration.Application.Tests;

public sealed class HandlerRegistrationTests : IClassFixture<BggIntegrationTestFixture>
{
	private readonly BggIntegrationTestContext _context;

	public HandlerRegistrationTests(BggIntegrationTestFixture fixture)
	{
		_context = fixture.CreateContext();
	}

	[Fact]
	public void EveryHandlerInTheApplicationAssembly_IsRegisteredInTheTestFixture()
	{
		HandlerRegistrationAssertions.AssertEveryHandlerIsRegistered(
			typeof(SearchBggQuery).Assembly, _context.ServiceProvider, nameof(BggIntegrationTestFixture));
	}
}