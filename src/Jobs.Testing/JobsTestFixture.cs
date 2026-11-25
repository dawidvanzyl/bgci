using Jobs.Application.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobs.Testing;

/// <summary>
/// Builds <see cref="JobsTestContext"/> instances. Use it as an xUnit class fixture
/// (<c>IClassFixture&lt;JobsTestFixture&gt;</c>) and call <see cref="CreateContext"/>
/// in the test constructor, so every test gets fresh mocks.
/// Register a new handler here when you add one to Jobs.Application. The handler
/// registration test fails until you do.
/// </summary>
public sealed class JobsTestFixture
{
	public JobsTestContext CreateContext()
	{
		return new JobsTestContext(context =>
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

	private static void RegisterRepositories(ServiceCollection services, JobsTestContext context)
	{
		services.AddTransient(_ => context.Repositories.BggWriteRequestRepositoryMock.Object);
	}

	private static void RegisterServices(ServiceCollection services, JobsTestContext context)
	{
		services.AddTransient(_ => context.Services.BggCollectionWriterMock.Object);
		services.AddTransient(_ => context.Services.MediatorMock.Object);
		services.AddTransient(_ => context.Services.BggTestWriteQueueMock.Object);
		services.AddTransient(_ => context.Services.BggTestCollectionReaderMock.Object);
		services.AddTransient(_ => context.Services.BggTestAccountMock.Object);
		services.AddTransient(_ => context.Services.LiveWritableGamesMock.Object);
		services.AddTransient(_ => context.Services.BggLiveTestLogMock.Object);
		services.AddSingleton<TimeProvider>(context.Time);
	}

	private static void RegisterHandlers(ServiceCollection services)
	{
		services.AddTransient<RunBggAddCommandHandler>();
		services.AddTransient<RunBggRemoveCommandHandler>();
		services.AddTransient<RunBggLiveTestCommandHandler>();
	}
}