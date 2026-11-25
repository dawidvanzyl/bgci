using Jobs.Application.Commands;
using Jobs.Application.Models;
using Jobs.Testing;
using Jobs.Testing.Factories;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using System.Text.Json;
using Xunit;

namespace Jobs.Application.Tests.Commands;

public sealed class RunBggRemoveCommandHandlerTests : IClassFixture<JobsTestFixture>
{
	private readonly JobsTestContext _context;
	private readonly RunBggRemoveCommandHandler _handler;

	public RunBggRemoveCommandHandlerTests(JobsTestFixture fixture)
	{
		_context = fixture.CreateContext();
		_handler = _context.ServiceProvider.GetRequiredService<RunBggRemoveCommandHandler>();
	}

	[Fact]
	[Trait("AC", "111UC7")]
	public async Task Handle_RequestHoldsTheCollectionItemId_RemovesIt()
	{
		var request = StoredRequest(collectionItemId: 777);

		await _handler.Handle(new RunBggRemoveCommand(request.Id, 0), CancellationToken.None);

		_context.Services.BggCollectionWriterMock.Verify(w => w.RemoveAsync("owner", 777, It.IsAny<CancellationToken>()), Times.Once);
		var completion = _context.Repositories.Completions().ShouldHaveSingleItem();
		completion.Status.ShouldBe(BggWriteRequestStatus.Sent);
		completion.CollectionItemId.ShouldBe(777);
	}

	[Fact]
	[Trait("AC", "111UC7")]
	public async Task Handle_RequestHasNoCollectionItemId_FindsItByBggIdAndRemovesThatId()
	{
		var request = StoredRequest(bggId: 230802);
		_context.Services.BggFindReturns(888);

		await _handler.Handle(new RunBggRemoveCommand(request.Id, 0), CancellationToken.None);

		_context.Services.BggCollectionWriterMock.Verify(w => w.FindCollectionItemIdAsync("owner", 230802, It.IsAny<CancellationToken>()), Times.Once);
		_context.Services.BggCollectionWriterMock.Verify(w => w.RemoveAsync("owner", 888, It.IsAny<CancellationToken>()), Times.Once);
		_context.Repositories.Completions().ShouldHaveSingleItem().CollectionItemId.ShouldBe(888);
	}

	[Theory]
	[InlineData(null)]
	[InlineData(777L)]
	[Trait("AC", "112UC5")]
	public async Task Handle_ItemNoLongerOnBgg_CompletesSentWithoutRemoving(long? collectionItemId)
	{
		var request = StoredRequest(bggId: 230802, collectionItemId: collectionItemId);
		_context.Services.BggFindReturns(null);

		var outcome = await _handler.Handle(new RunBggRemoveCommand(request.Id, 0), CancellationToken.None);

		outcome.ShouldBe(BggJobOutcome.Finished);
		_context.Services.BggCollectionWriterMock.Verify(w => w.RemoveAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
		var completion = _context.Repositories.Completions().ShouldHaveSingleItem();
		completion.Status.ShouldBe(BggWriteRequestStatus.Sent);
		completion.Result.ShouldBe("already gone from the BGG collection");
	}

	[Fact]
	[Trait("AC", "112UC5")]
	public async Task Handle_ItemListedAndRequestHoldsAnId_RemovesTheStoredId()
	{
		var request = StoredRequest(collectionItemId: 777);
		_context.Services.BggFindReturns(9999);

		await _handler.Handle(new RunBggRemoveCommand(request.Id, 0), CancellationToken.None);

		_context.Services.BggCollectionWriterMock.Verify(w => w.RemoveAsync("owner", 777, It.IsAny<CancellationToken>()), Times.Once);
		_context.Services.BggCollectionWriterMock.Verify(w => w.RemoveAsync("owner", 9999, It.IsAny<CancellationToken>()), Times.Never);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(2)]
	[Trait("AC", "112UC6")]
	public async Task Handle_EarlierRequestUnfinished_WaitsWithoutAnAttemptOrAnyCall(int retryCount)
	{
		var request = StoredRequest(collectionItemId: 777);
		_context.Repositories.HasUnfinishedEarlierRequest(request.Id, true);

		var outcome = await _handler.Handle(new RunBggRemoveCommand(request.Id, retryCount), CancellationToken.None);

		outcome.ShouldBe(BggJobOutcome.WaitingForEarlierRequest);
		_context.Services.BggCollectionWriterMock.VerifyNoOtherCalls();
		_context.Repositories.BggWriteRequestRepositoryMock.Verify(
			r => r.StartAttemptAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
		_context.Repositories.Completions().ShouldBeEmpty();
	}

	[Fact]
	[Trait("AC", "112UC6")]
	public async Task Handle_RequestCancelled_CallsNothing()
	{
		var request = StoredRequest(collectionItemId: 777, status: BggWriteRequestStatus.Cancelled);

		var outcome = await _handler.Handle(new RunBggRemoveCommand(request.Id, 0), CancellationToken.None);

		outcome.ShouldBe(BggJobOutcome.Finished);
		_context.Services.BggCollectionWriterMock.VerifyNoOtherCalls();
		_context.Repositories.Completions().ShouldBeEmpty();
	}

	[Fact]
	[Trait("AC", "111UC8")]
	public async Task Handle_BggFailsOnTheFinalAttempt_CompletesFailedAndRethrows()
	{
		var request = StoredRequest(collectionItemId: 777);
		_context.Services.BggCollectionWriterMock
			.Setup(w => w.RemoveAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("BGG down"));

		await Should.ThrowAsync<InvalidOperationException>(
			() => _handler.Handle(new RunBggRemoveCommand(request.Id, 2), CancellationToken.None));

		var completion = _context.Repositories.Completions().ShouldHaveSingleItem();
		completion.Status.ShouldBe(BggWriteRequestStatus.Failed);
		completion.Error.ShouldBe("BGG down");
		_context.Services.MediatorMock.VerifyNoOtherCalls();
	}

	[Fact]
	[Trait("AC", "111UC8")]
	public async Task Handle_LookupFailsOnTheFinalAttempt_CompletesFailed()
	{
		var request = StoredRequest(bggId: 230802);
		_context.Services.BggCollectionWriterMock
			.Setup(w => w.FindCollectionItemIdAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("BGG not ready"));

		await Should.ThrowAsync<InvalidOperationException>(
			() => _handler.Handle(new RunBggRemoveCommand(request.Id, 2), CancellationToken.None));

		_context.Repositories.Completions().ShouldHaveSingleItem().Status.ShouldBe(BggWriteRequestStatus.Failed);
	}

	[Fact]
	[Trait("AC", "111UC8")]
	public async Task Handle_RecordingTheAttemptFailsOnTheFinalAttempt_StillCompletesFailed()
	{
		var request = StoredRequest(collectionItemId: 777);
		_context.Repositories.BggWriteRequestRepositoryMock
			.Setup(r => r.StartAttemptAsync(request.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("jobs database down"));

		await Should.ThrowAsync<InvalidOperationException>(
			() => _handler.Handle(new RunBggRemoveCommand(request.Id, 2), CancellationToken.None));

		_context.Repositories.Completions().ShouldHaveSingleItem().Status.ShouldBe(BggWriteRequestStatus.Failed);
		_context.Services.BggCollectionWriterMock.VerifyNoOtherCalls();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[Trait("AC", "111UC8")]
	public async Task Handle_BggFailsOnAnEarlierAttempt_RethrowsAndCompletesNothing(int retryCount)
	{
		var request = StoredRequest(collectionItemId: 777);
		_context.Services.BggRemoveThrows(new InvalidOperationException("BGG down"));

		await Should.ThrowAsync<InvalidOperationException>(
			() => _handler.Handle(new RunBggRemoveCommand(request.Id, retryCount), CancellationToken.None));

		_context.Repositories.Completions().ShouldBeEmpty();
	}

	[Fact]
	[Trait("AC", "111UC10")]
	public async Task Handle_RequestAlreadySent_CallsNothing()
	{
		var request = StoredRequest(collectionItemId: 777, status: BggWriteRequestStatus.Sent);

		await _handler.Handle(new RunBggRemoveCommand(request.Id, 0), CancellationToken.None);

		_context.Services.BggCollectionWriterMock.VerifyNoOtherCalls();
		_context.Repositories.Completions().ShouldBeEmpty();
	}

	[Fact]
	[Trait("AC", "111UC16")]
	public async Task Handle_RequestNoLongerExists_CallsNothing()
	{
		await _handler.Handle(new RunBggRemoveCommand(Guid.NewGuid(), 0), CancellationToken.None);

		_context.Services.BggCollectionWriterMock.VerifyNoOtherCalls();
		_context.Repositories.Completions().ShouldBeEmpty();
	}

	[Fact]
	[Trait("AC", "111UC11")]
	public async Task Handle_Success_CompletesOnceWithTheLogFieldsAndASecretFreePayload()
	{
		var request = StoredRequest(collectionItemId: 777, bggId: 230802, attempts: 0);
		var start = _context.Time.GetUtcNow();

		await _handler.Handle(new RunBggRemoveCommand(request.Id, 0), CancellationToken.None);

		var completion = _context.Repositories.Completions().ShouldHaveSingleItem();
		completion.JobType.ShouldBe("BGG remove");
		completion.Account.ShouldBe("owner");
		completion.Result.ShouldNotBeNullOrWhiteSpace();
		completion.Error.ShouldBeNull();
		completion.Attempts.ShouldBe(1);
		completion.StartedAt.ShouldBe(start);
		using var payload = JsonDocument.Parse(completion.Payload);
		payload.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray()
			.ShouldBe(["account", "bggId", "collectionItemId", "gameId", "kind", "name", "requestId"]);
		payload.RootElement.GetProperty("kind").GetString().ShouldBe("remove");
		payload.RootElement.GetProperty("collectionItemId").GetInt64().ShouldBe(777);
	}

	[Fact]
	[Trait("AC", "111UC11")]
	public async Task Handle_FinalFailure_CompletesOnceWithTheError()
	{
		var request = StoredRequest(collectionItemId: 777);
		_context.Services.BggRemoveThrows(new InvalidOperationException("BGG remove failed with status 500: ***"));

		await Should.ThrowAsync<InvalidOperationException>(
			() => _handler.Handle(new RunBggRemoveCommand(request.Id, 2), CancellationToken.None));

		var completion = _context.Repositories.Completions().ShouldHaveSingleItem();
		completion.JobType.ShouldBe("BGG remove");
		completion.Error.ShouldBe("BGG remove failed with status 500: ***");
		completion.Result.ShouldBeNull();
	}

	private BggWriteRequest StoredRequest(
		int bggId = BggWriteRequestFactory.DefaultBggId,
		long? collectionItemId = null,
		BggWriteRequestStatus status = BggWriteRequestStatus.Queued,
		int attempts = 0)
	{
		var request = BggWriteRequestFactory.CreateRemove(bggId: bggId, collectionItemId: collectionItemId, status: status, attempts: attempts);
		_context.Repositories.HasStored(request);
		_context.Services.BggFindReturns(collectionItemId ?? 888);
		return request;
	}
}