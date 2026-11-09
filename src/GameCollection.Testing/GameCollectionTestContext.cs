using GameCollection.Application.Abstractions;
using GameCollection.Domain.Aggregates;
using GameCollection.Domain.Repositories;
using Moq;

namespace GameCollection.Testing;

/// <summary>
/// One isolated set of fakes and one service provider per test. Tests configure the mocks,
/// then resolve the system under test from <see cref="ServiceProvider"/>.
/// </summary>
public sealed class GameCollectionTestContext
{
	public GameCollectionTestContext(Func<GameCollectionTestContext, IServiceProvider> serviceProviderFactory)
	{
		ServiceProvider = serviceProviderFactory(this)
			?? throw new ArgumentNullException(nameof(serviceProviderFactory));
	}

	public RepositoryMocks Repositories { get; } = new();

	public ServiceMocks Services { get; } = new();

	public IServiceProvider ServiceProvider { get; }

	public sealed class RepositoryMocks
	{
		public Mock<ICollectedGameRepository> CollectedGameRepositoryMock { get; } = new();

		/// <summary>Makes the repository return <paramref name="game"/> when asked for its id.</summary>
		public void HasStored(CollectedGame game)
		{
			CollectedGameRepositoryMock
				.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
				.ReturnsAsync(game);
		}
	}

	public sealed class ServiceMocks
	{
		public Mock<IBggCollectionWriter> BggCollectionWriterMock { get; } = new();

		public void BggAddReturns(BggAddResult result)
		{
			BggCollectionWriterMock
				.Setup(w => w.AddToCollectionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(result);
		}

		public void BggAddThrows(Exception exception)
		{
			BggCollectionWriterMock
				.Setup(w => w.AddToCollectionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
				.ThrowsAsync(exception);
		}

		public void BggRemoveReturns(BggWriteOutcome outcome)
		{
			BggCollectionWriterMock
				.Setup(w => w.RemoveFromCollectionAsync(It.IsAny<long?>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(outcome);
		}

		public void BggRemoveThrows(Exception exception)
		{
			BggCollectionWriterMock
				.Setup(w => w.RemoveFromCollectionAsync(It.IsAny<long?>(), It.IsAny<CancellationToken>()))
				.ThrowsAsync(exception);
		}
	}
}