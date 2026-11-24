using GameCollection.Application.Commands;
using GameCollection.Application.Queries;
using Jobs.Application.Abstractions;
using Jobs.Application.Models;
using MediatR;

namespace Jobs.Application.Commands;

public class RunBggAddCommandHandler : IRequestHandler<RunBggAddCommand>
{
	public const string JobType = "BGG add";

	private readonly IBggWriteRequestRepository _requests;
	private readonly IBggCollectionWriter _writer;
	private readonly IMediator _mediator;
	private readonly TimeProvider _timeProvider;

	public RunBggAddCommandHandler(
		IBggWriteRequestRepository requests,
		IBggCollectionWriter writer,
		IMediator mediator,
		TimeProvider timeProvider)
	{
		_requests = requests;
		_writer = writer;
		_mediator = mediator;
		_timeProvider = timeProvider;
	}

	public async Task Handle(RunBggAddCommand command, CancellationToken cancellationToken)
	{
		var request = await _requests.GetAsync(command.RequestId, cancellationToken);
		if (request is null || request.Status == BggWriteRequestStatus.Sent)
			return;

		var startedAt = _timeProvider.GetUtcNow();
		var attempts = request.Attempts;
		try
		{
			attempts = await _requests.RecordAttemptAsync(request.Id, startedAt, cancellationToken);
			await AddAsync(request, attempts, startedAt, cancellationToken);
		}
		catch (Exception ex) when (BggJobPolicy.IsFinalAttempt(command.RetryCount) && !IsCancelled(ex, cancellationToken))
		{
			await _mediator.Send(new MarkGameBggFailedCommand(request.GameId), cancellationToken);
			await _requests.CompleteAsync(
				BggWriteCompletions.Failed(request, JobType, ex.Message, attempts, startedAt, _timeProvider.GetUtcNow()),
				cancellationToken);
			throw;
		}
	}

	private async Task AddAsync(BggWriteRequest request, int attempts, DateTimeOffset startedAt, CancellationToken cancellationToken)
	{
		var game = await _mediator.Send(new GetGameByIdQuery(request.GameId), cancellationToken);
		if (game?.BggStatus == "sent")
		{
			await _requests.CompleteAsync(
				BggWriteCompletions.Sent(request, JobType, null, "game already sent", attempts, startedAt, _timeProvider.GetUtcNow()),
				cancellationToken);
			return;
		}

		var collectionItemId = await _writer.AddAsync(request.BggId, request.Name, cancellationToken);
		await _mediator.Send(new MarkGameBggSentCommand(request.GameId, collectionItemId), cancellationToken);
		await _requests.CompleteAsync(
			BggWriteCompletions.Sent(request, JobType, collectionItemId, $"added as collection item {collectionItemId}", attempts, startedAt, _timeProvider.GetUtcNow()),
			cancellationToken);
	}

	private static bool IsCancelled(Exception exception, CancellationToken cancellationToken) =>
		exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
}