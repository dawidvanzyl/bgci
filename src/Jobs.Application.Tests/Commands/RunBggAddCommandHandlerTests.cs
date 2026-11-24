using GameCollection.Application.Commands;
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

public sealed class RunBggAddCommandHandlerTests : IClassFixture<JobsTestFixture>
{
	private readonly JobsTestContext _context;
	private readonly RunBggAddCommandHandler _handler;

	public RunBggAddCommandHandlerTests(JobsTestFixture fixture)
	{
		_context = fixture.CreateContext();
		_handler = _context.ServiceProvider.GetRequiredService<RunBggAddCommandHandler>();
	}

	[Fact]
	[Trait("AC", "111UC6")]
	public async Task Handle_BggAccepts_MarksTheGameSentThenCompletesTheRequestSent()
	{
		var request = StoredRequest();
		_context.Services.GameIs(CollectedGameDtoFactory.CreateFromBgg(request.GameId));
		_context.Services.BggAddReturns(555);
		_context.Services.MarkSentReturns(true);

		await _handler.Handle(new RunBggAddCommand(request.Id, 0), CancellationToken.None);

		_context.Services.BggCollectionWriterMock.Verify(w => w.AddAsync(request.BggId, request.Name, It.IsAny<CancellationToken>()), Times.Once);
		_context.Services.MediatorMock.Verify(m => m.Send(new MarkGameBggSentCommand(request.GameId, 555), It.IsAny<CancellationToken>()), Times.Once);
		var completion = _context.Repositories.Completions().ShouldHaveSingleItem();
		completion.Status.ShouldBe(BggWriteRequestStatus.Sent);
		completion.CollectionItemId.ShouldBe(555);
	}

	[Fact]
	[Trait("AC", "112UC3")]
	public async Task Handle_GameRemovedMeanwhile_CompletesCancelledWithoutCallingBgg()
	{
		var request = StoredRequest();
		_context.Services.GameIs(null);

		var outcome = await _handler.Handle(new RunBggAddCommand(request.Id, 0), CancellationToken.None);

		outcome.ShouldBe(BggJobOutcome.Finished);
		_context.Services.BggCollectionWriterMock.VerifyNoOtherCalls();
		_context.Repositories.Completions().ShouldHaveSingleItem().Status.ShouldBe(BggWriteRequestStatus.Cancelled);
	}

	[Fact]
	[Trait("AC", "112UC3")]
	public async Task Handle_RequestCancelled_CallsNothingAndStartsNoAttempt()
	{
		var request = StoredRequest(status: BggWriteRequestStatus.Cancelled);

		var outcome = await _handler.Handle(new RunBggAddCommand(request.Id, 0), CancellationToken.None);

		outcome.ShouldBe(BggJobOutcome.Finished);
		_context.Services.BggCollectionWriterMock.VerifyNoOtherCalls();
		_context.Services.MediatorMock.VerifyNoOtherCalls();
		_context.Repositories.BggWriteRequestRepositoryMock.Verify(
			r => r.StartAttemptAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
		_context.Repositories.Completions().ShouldBeEmpty();
	}

	[Fact]
	[Trait("AC", "112UC3")]
	public async Task Handle_RequestCancelledBetweenTheReadAndTheAttempt_CallsNothingOnBgg()
	{
		var request = StoredRequest();
		_context.Repositories.BggWriteRequestRepositoryMock
			.Setup(r => r.StartAttemptAsync(request.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((int?)null);

		var outcome = await _handler.Handle(new RunBggAddCommand(request.Id, 0), CancellationToken.None);

		outcome.ShouldBe(BggJobOutcome.Finished);
		_context.Services.BggCollectionWriterMock.VerifyNoOtherCalls();
		_context.Repositories.Completions().ShouldBeEmpty();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(2)]
	[Trait("AC", "112UC4")]
	public async Task Handle_ItemAlreadyOnBgg_MarksTheGameSentWithItAndDoesNotAddAgain(int retryCount)
	{
		var request = StoredRequest(attempts: retryCount);
		_context.Services.GameIs(CollectedGameDtoFactory.CreateFromBgg(request.GameId));
		_context.Services.BggFindReturns(4242);
		_context.Services.MarkSentReturns(true);

		var outcome = await _handler.Handle(new RunBggAddCommand(request.Id, retryCount), CancellationToken.None);

		outcome.ShouldBe(BggJobOutcome.Finished);
		_context.Services.BggCollectionWriterMock.Verify(w => w.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
		_context.Services.MediatorMock.Verify(m => m.Send(new MarkGameBggSentCommand(request.GameId, 4242), It.IsAny<CancellationToken>()), Times.Once);
		var completion = _context.Repositories.Completions().ShouldHaveSingleItem();
		completion.Status.ShouldBe(BggWriteRequestStatus.Sent);
		completion.CollectionItemId.ShouldBe(4242);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(2)]
	[Trait("AC", "112UC6")]
	public async Task Handle_EarlierRequestUnfinished_WaitsWithoutAnAttemptOrAnyCall(int retryCount)
	{
		var request = StoredRequest();
		_context.Repositories.HasUnfinishedEarlierRequest(request.Id, true);

		var outcome = await _handler.Handle(new RunBggAddCommand(request.Id, retryCount), CancellationToken.None);

		outcome.ShouldBe(BggJobOutcome.WaitingForEarlierRequest);
		_context.Services.BggCollectionWriterMock.VerifyNoOtherCalls();
		_context.Services.MediatorMock.VerifyNoOtherCalls();
		_context.Repositories.BggWriteRequestRepositoryMock.Verify(
			r => r.StartAttemptAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
		_context.Repositories.Completions().ShouldBeEmpty();
	}

	[Theory]
	[InlineData(2)]
	[InlineData(5)]
	[Trait("AC", "111UC8")]
	public async Task Handle_BggFailsOnTheFinalAttempt_MarksTheGameFailedCompletesFailedAndRethrows(int retryCount)
	{
		var request = StoredRequest();
		_context.Services.GameIs(CollectedGameDtoFactory.CreateFromBgg(request.GameId));
		_context.Services.BggAddThrows(new InvalidOperationException("BGG down"));

		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => _handler.Handle(new RunBggAddCommand(request.Id, retryCount), CancellationToken.None));

		exception.Message.ShouldBe("BGG down");
		_context.Services.MediatorMock.Verify(m => m.Send(new MarkGameBggFailedCommand(request.GameId), It.IsAny<CancellationToken>()), Times.Once);
		var completion = _context.Repositories.Completions().ShouldHaveSingleItem();
		completion.Status.ShouldBe(BggWriteRequestStatus.Failed);
		completion.Error.ShouldBe("BGG down");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[Trait("AC", "111UC8")]
	public async Task Handle_BggFailsOnAnEarlierAttempt_RethrowsAndMarksNothing(int retryCount)
	{
		var request = StoredRequest();
		_context.Services.GameIs(CollectedGameDtoFactory.CreateFromBgg(request.GameId));
		_context.Services.BggAddThrows(new InvalidOperationException("BGG down"));

		await Should.ThrowAsync<InvalidOperationException>(
			() => _handler.Handle(new RunBggAddCommand(request.Id, retryCount), CancellationToken.None));

		_context.Services.MediatorMock.Verify(m => m.Send(It.IsAny<MarkGameBggFailedCommand>(), It.IsAny<CancellationToken>()), Times.Never);
		_context.Repositories.Completions().ShouldBeEmpty();
	}

	[Fact]
	[Trait("AC", "111UC8")]
	public async Task Handle_RecordingTheAttemptFailsOnTheFinalAttempt_StillMarksTheGameFailedAndCompletesFailed()
	{
		var request = StoredRequest();
		_context.Repositories.BggWriteRequestRepositoryMock
			.Setup(r => r.StartAttemptAsync(request.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("jobs database down"));

		await Should.ThrowAsync<InvalidOperationException>(
			() => _handler.Handle(new RunBggAddCommand(request.Id, 2), CancellationToken.None));

		_context.Services.MediatorMock.Verify(m => m.Send(new MarkGameBggFailedCommand(request.GameId), It.IsAny<CancellationToken>()), Times.Once);
		_context.Repositories.Completions().ShouldHaveSingleItem().Status.ShouldBe(BggWriteRequestStatus.Failed);
		_context.Services.BggCollectionWriterMock.VerifyNoOtherCalls();
	}

	[Fact]
	[Trait("AC", "111UC8")]
	public async Task Handle_TheGameQueryFailsOnTheFinalAttempt_StillMarksTheGameFailedAndCompletesFailed()
	{
		var request = StoredRequest();
		_context.Services.GameQueryThrows(new InvalidOperationException("bgci database down"));

		await Should.ThrowAsync<InvalidOperationException>(
			() => _handler.Handle(new RunBggAddCommand(request.Id, 2), CancellationToken.None));

		_context.Services.MediatorMock.Verify(m => m.Send(new MarkGameBggFailedCommand(request.GameId), It.IsAny<CancellationToken>()), Times.Once);
		_context.Repositories.Completions().ShouldHaveSingleItem().Status.ShouldBe(BggWriteRequestStatus.Failed);
	}

	[Fact]
	[Trait("AC", "111UC8")]
	public async Task Handle_MarkingFailedFailsOnTheFinalAttempt_ThatExceptionReplacesTheOriginalAndNothingIsCompleted()
	{
		var request = StoredRequest();
		_context.Services.GameIs(CollectedGameDtoFactory.CreateFromBgg(request.GameId));
		_context.Services.BggAddThrows(new InvalidOperationException("BGG down"));
		_context.Services.MediatorMock
			.Setup(m => m.Send(It.IsAny<MarkGameBggFailedCommand>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new TimeoutException("bgci database down"));

		await Should.ThrowAsync<TimeoutException>(
			() => _handler.Handle(new RunBggAddCommand(request.Id, 2), CancellationToken.None));

		_context.Repositories.Completions().ShouldBeEmpty();
	}

	[Fact]
	[Trait("AC", "111UC8")]
	public async Task Handle_ShutdownCancelsTheJobOnTheFinalAttempt_MarksNothing()
	{
		var request = StoredRequest();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();
		_context.Services.GameIs(CollectedGameDtoFactory.CreateFromBgg(request.GameId));
		_context.Services.BggAddThrows(new OperationCanceledException(cts.Token));

		await Should.ThrowAsync<OperationCanceledException>(
			() => _handler.Handle(new RunBggAddCommand(request.Id, 2), cts.Token));

		_context.Services.MediatorMock.Verify(m => m.Send(It.IsAny<MarkGameBggFailedCommand>(), It.IsAny<CancellationToken>()), Times.Never);
		_context.Repositories.Completions().ShouldBeEmpty();
	}

	[Fact]
	[Trait("AC", "111UC9")]
	public async Task Handle_FailedAddRetriedAndAccepted_MarksTheGameSentAndCompletesTheRequestSent()
	{
		var request = StoredRequest(status: BggWriteRequestStatus.Failed, attempts: 3);
		_context.Services.GameIs(CollectedGameDtoFactory.CreateFromBgg(request.GameId, bggStatus: "failed"));
		_context.Services.BggAddReturns(777);
		_context.Services.MarkSentReturns(true);

		await _handler.Handle(new RunBggAddCommand(request.Id, 2), CancellationToken.None);

		_context.Services.MediatorMock.Verify(m => m.Send(new MarkGameBggSentCommand(request.GameId, 777), It.IsAny<CancellationToken>()), Times.Once);
		var completion = _context.Repositories.Completions().ShouldHaveSingleItem();
		completion.Status.ShouldBe(BggWriteRequestStatus.Sent);
		completion.Attempts.ShouldBe(4);
	}

	[Fact]
	[Trait("AC", "111UC10")]
	public async Task Handle_GameAlreadySent_DoesNotCallBggAndCompletesTheRequestSent()
	{
		var request = StoredRequest();
		_context.Services.GameIs(CollectedGameDtoFactory.CreateFromBgg(request.GameId, bggStatus: "sent"));

		await _handler.Handle(new RunBggAddCommand(request.Id, 0), CancellationToken.None);

		_context.Services.BggCollectionWriterMock.VerifyNoOtherCalls();
		_context.Services.MediatorMock.Verify(m => m.Send(It.IsAny<MarkGameBggSentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
		_context.Repositories.Completions().ShouldHaveSingleItem().Status.ShouldBe(BggWriteRequestStatus.Sent);
	}

	[Fact]
	[Trait("AC", "111UC10")]
	public async Task Handle_RequestAlreadySent_CallsNothing()
	{
		var request = StoredRequest(status: BggWriteRequestStatus.Sent);

		await _handler.Handle(new RunBggAddCommand(request.Id, 0), CancellationToken.None);

		_context.Services.BggCollectionWriterMock.VerifyNoOtherCalls();
		_context.Services.MediatorMock.VerifyNoOtherCalls();
		_context.Repositories.BggWriteRequestRepositoryMock.Verify(
			r => r.StartAttemptAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
		_context.Repositories.Completions().ShouldBeEmpty();
	}

	[Fact]
	[Trait("AC", "111UC16")]
	public async Task Handle_RequestNoLongerExists_CallsNothing()
	{
		await _handler.Handle(new RunBggAddCommand(Guid.NewGuid(), 0), CancellationToken.None);

		_context.Services.BggCollectionWriterMock.VerifyNoOtherCalls();
		_context.Services.MediatorMock.VerifyNoOtherCalls();
		_context.Repositories.Completions().ShouldBeEmpty();
	}

	[Fact]
	[Trait("AC", "111UC11")]
	public async Task Handle_Success_CompletesOnceWithTheLogFieldsAndASecretFreePayload()
	{
		var request = StoredRequest(name: "Azul", bggId: 230802, attempts: 1);
		_context.Services.GameIs(CollectedGameDtoFactory.CreateFromBgg(request.GameId));
		_context.Services.BggAddReturns(555);
		_context.Services.MarkSentReturns(true);
		var start = _context.Time.GetUtcNow();

		await _handler.Handle(new RunBggAddCommand(request.Id, 1), CancellationToken.None);

		var completion = _context.Repositories.Completions().ShouldHaveSingleItem();
		completion.JobType.ShouldBe("BGG add");
		completion.Account.ShouldBe("owner");
		completion.Result.ShouldNotBeNullOrWhiteSpace();
		completion.Error.ShouldBeNull();
		completion.Attempts.ShouldBe(2);
		completion.StartedAt.ShouldBe(start);
		completion.EndedAt.ShouldBeGreaterThanOrEqualTo(completion.StartedAt);
		PayloadKeys(completion).ShouldBe(["account", "bggId", "collectionItemId", "gameId", "kind", "name", "requestId"]);
		using var payload = JsonDocument.Parse(completion.Payload);
		payload.RootElement.GetProperty("kind").GetString().ShouldBe("add");
		payload.RootElement.GetProperty("bggId").GetInt32().ShouldBe(230802);
		payload.RootElement.GetProperty("name").GetString().ShouldBe("Azul");
	}

	[Fact]
	[Trait("AC", "111UC11")]
	public async Task Handle_FinalFailure_CompletesOnceWithTheErrorAndASecretFreePayload()
	{
		var request = StoredRequest();
		_context.Services.GameIs(CollectedGameDtoFactory.CreateFromBgg(request.GameId));
		_context.Services.BggAddThrows(new InvalidOperationException("BGG add failed with status 500: ***"));

		await Should.ThrowAsync<InvalidOperationException>(
			() => _handler.Handle(new RunBggAddCommand(request.Id, 2), CancellationToken.None));

		var completion = _context.Repositories.Completions().ShouldHaveSingleItem();
		completion.JobType.ShouldBe("BGG add");
		completion.Account.ShouldBe("owner");
		completion.Error.ShouldBe("BGG add failed with status 500: ***");
		completion.Result.ShouldBeNull();
		PayloadKeys(completion).ShouldBe(["account", "bggId", "collectionItemId", "gameId", "kind", "name", "requestId"]);
	}

	private BggWriteRequest StoredRequest(
		string name = "Catan",
		int bggId = BggWriteRequestFactory.DefaultBggId,
		BggWriteRequestStatus status = BggWriteRequestStatus.Queued,
		int attempts = 0)
	{
		var request = BggWriteRequestFactory.CreateAdd(name: name, bggId: bggId, status: status, attempts: attempts);
		_context.Repositories.HasStored(request);
		return request;
	}

	private static string[] PayloadKeys(BggWriteCompletion completion)
	{
		using var payload = JsonDocument.Parse(completion.Payload);
		return payload.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();
	}
}