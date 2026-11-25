using BggIntegration.Domain.Interfaces;
using GameCollection.Application.DTOs;
using GameCollection.Application.Queries;
using MediatR;
using Moq;

namespace BggIntegration.Testing;

/// <summary>
/// One isolated set of fakes and one service provider per test. Tests configure the mocks,
/// then resolve the system under test from <see cref="ServiceProvider"/>.
/// </summary>
public sealed class BggIntegrationTestContext
{
	public BggIntegrationTestContext(Func<BggIntegrationTestContext, IServiceProvider> serviceProviderFactory)
	{
		ServiceProvider = serviceProviderFactory(this)
			?? throw new ArgumentNullException(nameof(serviceProviderFactory));
	}

	public ClientMocks Clients { get; } = new();

	public ServiceMocks Services { get; } = new();

	public IServiceProvider ServiceProvider { get; }

	public sealed class ClientMocks
	{
		public Mock<IBggClient> BggClientMock { get; } = new();
	}

	public sealed class ServiceMocks
	{
		public Mock<IMediator> MediatorMock { get; } = new();

		public void HasLocalGames(params CollectedGameDto[] games)
		{
			MediatorMock
				.Setup(m => m.Send(It.IsAny<GetAllGamesQuery>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(games);
		}

		public void HasPendingRemoves(params int[] bggIds)
		{
			MediatorMock
				.Setup(m => m.Send(It.IsAny<GetPendingBggRemovesQuery>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(bggIds);
		}
	}
}