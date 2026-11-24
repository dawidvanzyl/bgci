using Jobs.Application.Abstractions;
using Jobs.Application.Models;
using MediatR;

namespace Jobs.Application.Commands;

public class RunBggRemoveCommandHandler : IRequestHandler<RunBggRemoveCommand>
{
	public const string JobType = "BGG remove";

	private readonly IBggWriteRequestRepository _requests;
	private readonly IBggCollectionWriter _writer;
	private readonly TimeProvider _timeProvider;

	public RunBggRemoveCommandHandler(
		IBggWriteRequestRepository requests,
		IBggCollectionWriter writer,
		TimeProvider timeProvider)
	{
		_requests = requests;
		_writer = writer;
		_timeProvider = timeProvider;
	}

	public async Task Handle(RunBggRemoveCommand command, CancellationToken cancellationToken)
	{
		var request = await _requests.GetAsync(command.RequestId, cancellationToken);
		if (request is null || request.Status == BggWriteRequestStatus.Sent)
			return;

		var startedAt = _timeProvider.GetUtcNow();
		var attempts = request.Attempts;
		try
		{
			attempts = await _requests.RecordAttemptAsync(request.Id, startedAt, cancellationToken);
			await RemoveAsync(request, attempts, startedAt, cancellationToken);
		}
		catch (Exception ex) when (BggJobPolicy.IsFinalAttempt(command.RetryCount) && !IsCancelled(ex, cancellationToken))
		{
			await _requests.CompleteAsync(
				BggWriteCompletions.Failed(request, JobType, ex.Message, attempts, startedAt, _timeProvider.GetUtcNow()),
				cancellationToken);
			throw;
		}
	}

	private async Task RemoveAsync(BggWriteRequest request, int attempts, DateTimeOffset startedAt, CancellationToken cancellationToken)
	{
		var collectionItemId = request.CollectionItemId
			?? await _writer.FindCollectionItemIdAsync(request.BggId, cancellationToken)
			?? throw new BggItemNotFoundException(request.BggId);

		await _writer.RemoveAsync(collectionItemId, cancellationToken);
		await _requests.CompleteAsync(
			BggWriteCompletions.Sent(request, JobType, collectionItemId, $"removed collection item {collectionItemId}", attempts, startedAt, _timeProvider.GetUtcNow()),
			cancellationToken);
	}

	private static bool IsCancelled(Exception exception, CancellationToken cancellationToken) =>
		exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
}