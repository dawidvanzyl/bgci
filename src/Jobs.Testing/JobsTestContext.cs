using GameCollection.Application.Commands;
using GameCollection.Application.DTOs;
using GameCollection.Application.Queries;
using Jobs.Application.Abstractions;
using Jobs.Application.Models;
using MediatR;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Jobs.Testing;

/// <summary>
/// One isolated set of fakes and one service provider per test. Tests configure the mocks,
/// then resolve the system under test from <see cref="ServiceProvider"/>.
/// </summary>
public sealed class JobsTestContext
{
	public JobsTestContext(Func<JobsTestContext, IServiceProvider> serviceProviderFactory)
	{
		ServiceProvider = serviceProviderFactory(this)
			?? throw new ArgumentNullException(nameof(serviceProviderFactory));
	}

	public RepositoryMocks Repositories { get; } = new();

	public ServiceMocks Services { get; } = new();

	public FakeTimeProvider Time { get; } = new();

	public IServiceProvider ServiceProvider { get; }

	public sealed class RepositoryMocks
	{
		public Mock<IBggWriteRequestRepository> BggWriteRequestRepositoryMock { get; } = new();

		/// <summary>Makes the repository return <paramref name="request"/> when asked for its id, and count one more attempt than it holds.</summary>
		public void HasStored(BggWriteRequest request)
		{
			BggWriteRequestRepositoryMock
				.Setup(r => r.GetAsync(request.Id, It.IsAny<CancellationToken>()))
				.ReturnsAsync(request);
			BggWriteRequestRepositoryMock
				.Setup(r => r.StartAttemptAsync(request.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(request.Attempts + 1);
		}

		public void HasUnfinishedEarlierRequest(Guid requestId, bool value)
		{
			BggWriteRequestRepositoryMock
				.Setup(r => r.HasUnfinishedEarlierRequestAsync(requestId, It.IsAny<CancellationToken>()))
				.ReturnsAsync(value);
		}

		public IReadOnlyList<BggWriteCompletion> Completions() =>
			BggWriteRequestRepositoryMock.Invocations
				.Where(i => i.Method.Name == nameof(IBggWriteRequestRepository.CompleteAsync))
				.Select(i => (BggWriteCompletion)i.Arguments[0])
				.ToList();
	}

	public sealed class ServiceMocks
	{
		public Mock<IBggCollectionWriter> BggCollectionWriterMock { get; } = new();

		public Mock<IBggTestWriteQueue> BggTestWriteQueueMock { get; } = new();

		public Mock<IBggTestCollectionReader> BggTestCollectionReaderMock { get; } = new();

		public Mock<IBggTestAccount> BggTestAccountMock { get; } = new();

		public Mock<ILiveWritableGames> LiveWritableGamesMock { get; } = new();

		public Mock<IBggLiveTestLog> BggLiveTestLogMock { get; } = new();

		public Mock<IMediator> MediatorMock { get; } = new();

		public void GameIs(CollectedGameDto? game)
		{
			MediatorMock
				.Setup(m => m.Send(It.IsAny<GetGameByIdQuery>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(game);
		}

		public void GameQueryThrows(Exception exception)
		{
			MediatorMock
				.Setup(m => m.Send(It.IsAny<GetGameByIdQuery>(), It.IsAny<CancellationToken>()))
				.ThrowsAsync(exception);
		}

		public void MarkSentReturns(bool found)
		{
			MediatorMock
				.Setup(m => m.Send(It.IsAny<MarkGameBggSentCommand>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(found);
		}

		public void MarkFailedReturns(bool found)
		{
			MediatorMock
				.Setup(m => m.Send(It.IsAny<MarkGameBggFailedCommand>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(found);
		}

		public void BggAddReturns(long collectionItemId)
		{
			BggCollectionWriterMock
				.Setup(w => w.AddAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(collectionItemId);
		}

		public void BggAddThrows(Exception exception)
		{
			BggCollectionWriterMock
				.Setup(w => w.AddAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ThrowsAsync(exception);
		}

		public void BggFindReturns(long? collectionItemId)
		{
			BggCollectionWriterMock
				.Setup(w => w.FindCollectionItemIdAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(collectionItemId);
		}

		public void BggRemoveThrows(Exception exception)
		{
			BggCollectionWriterMock
				.Setup(w => w.RemoveAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
				.ThrowsAsync(exception);
		}
	}
}