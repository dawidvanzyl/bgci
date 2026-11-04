using GameCollection.Application.Commands;
using GameCollection.Application.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameCollection.Testing;

/// <summary>
/// Builds <see cref="GameCollectionTestContext"/> instances. Use it as an xUnit class fixture
/// (<c>IClassFixture&lt;GameCollectionTestFixture&gt;</c>) and call <see cref="CreateContext"/>
/// in the test constructor, so every test gets fresh mocks.
/// Register a new handler here when you add one to GameCollection.Application. The handler
/// registration test fails until you do.
/// </summary>
public sealed class GameCollectionTestFixture
{
	public GameCollectionTestContext CreateContext()
	{
		return new GameCollectionTestContext(context =>
		{
			var services = new ServiceCollection();

			RegisterLogging(services);
			RegisterRepositories(services, context);
			RegisterServices(services, context);
			RegisterHandlers(services);

			return services.BuildServiceProvider();
		});
	}

	private static void RegisterLogging(ServiceCollection services)
	{
		services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
	}

	private static void RegisterRepositories(ServiceCollection services, GameCollectionTestContext context)
	{
		services.AddTransient(_ => context.Repositories.CollectedGameRepositoryMock.Object);
	}

	private static void RegisterServices(ServiceCollection services, GameCollectionTestContext context)
	{
		services.AddTransient(_ => context.Services.BggCollectionWriterMock.Object);
	}

	private static void RegisterHandlers(ServiceCollection services)
	{
		services.AddTransient<AddGameFromBggCommandHandler>();
		services.AddTransient<AddGameManuallyCommandHandler>();
		services.AddTransient<DeleteGameCommandHandler>();
		services.AddTransient<UpdateGameCommandHandler>();
		services.AddTransient<GetAllGamesQueryHandler>();
		services.AddTransient<GetGameByIdQueryHandler>();
	}
}