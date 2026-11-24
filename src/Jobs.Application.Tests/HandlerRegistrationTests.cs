using Jobs.Application.Commands;
using Jobs.Testing;
using Shared.Testing;
using Xunit;

namespace Jobs.Application.Tests;

public sealed class HandlerRegistrationTests : IClassFixture<JobsTestFixture>
{
	private readonly JobsTestContext _context;

	public HandlerRegistrationTests(JobsTestFixture fixture)
	{
		_context = fixture.CreateContext();
	}

	[Fact]
	public void EveryHandlerInTheApplicationAssembly_IsRegisteredInTheTestFixture()
	{
		HandlerRegistrationAssertions.AssertEveryHandlerIsRegistered(
			typeof(RunBggAddCommand).Assembly, _context.ServiceProvider, nameof(JobsTestFixture));
	}
}