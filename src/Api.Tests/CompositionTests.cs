using BggIntegration.Infrastructure.Http;
using GameCollection.Application.Abstractions;
using GameCollection.Testing.Postgres;
using Jobs.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Api.Tests;

public sealed class CompositionTests
{
	private readonly PostgresContainerFixture _postgres;

	public CompositionTests(PostgresContainerFixture postgres)
	{
		_postgres = postgres;
	}

	[Fact]
	[Trait("AC", "111UC12")]
	public async Task ComposedApi_HasNoBggWriteClientNoSessionAndNoCollectionWriter()
	{
		await using var factory = await StartAsync();

		using var scope = factory.Services.CreateScope();
		scope.ServiceProvider.GetService<IBggWriteClient>().ShouldBeNull();
		scope.ServiceProvider.GetService<BggSession>().ShouldBeNull();
		scope.ServiceProvider.GetService<IBggCollectionWriter>().ShouldBeNull();
		scope.ServiceProvider.GetService<IBggWriteQueue>().ShouldNotBeNull();
	}

	private async Task<ApiFactory> StartAsync()
	{
		var factory = new ApiFactory(await _postgres.CreateDatabaseAsync(), await _postgres.CreateDatabaseAsync());
		factory.CreateClient().Dispose();
		return factory;
	}
}
