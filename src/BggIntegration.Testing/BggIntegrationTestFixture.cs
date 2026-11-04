using BggIntegration.Application.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BggIntegration.Testing;

/// <summary>
/// Builds <see cref="BggIntegrationTestContext"/> instances. Use it as an xUnit class fixture
/// (<c>IClassFixture&lt;BggIntegrationTestFixture&gt;</c>) and call <see cref="CreateContext"/>
/// in the test constructor, so every test gets fresh mocks.
/// Register a new handler here when you add one to BggIntegration.Application. The handler
/// registration test fails until you do.
/// </summary>
public sealed class BggIntegrationTestFixture
{
	public BggIntegrationTestContext CreateContext()
	{
		return new BggIntegrationTestContext(context =>
		{
			var services = new ServiceCollection();

			RegisterLogging(services);
			RegisterClients(services, context);
			RegisterHandlers(services);

			return services.BuildServiceProvider();
		});
	}

	private static void RegisterLogging(ServiceCollection services)
	{
		services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
	}

	private static void RegisterClients(ServiceCollection services, BggIntegrationTestContext context)
	{
		services.AddTransient(_ => context.Clients.BggClientMock.Object);
	}

	private static void RegisterHandlers(ServiceCollection services)
	{
		services.AddTransient<GetBggCollectionQueryHandler>();
		services.AddTransient<GetBggExpansionsQueryHandler>();
		services.AddTransient<GetBggGameDetailsQueryHandler>();
		services.AddTransient<SearchBggQueryHandler>();
	}
}