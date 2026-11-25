using Jobs.Application.Models;
using System.Text.Json;

namespace Jobs.Application.Commands;

internal static class BggWriteCompletions
{
	public static BggWriteCompletion Sent(
		BggWriteRequest request,
		string jobType,
		long? collectionItemId,
		string result,
		int attempts,
		DateTimeOffset startedAt,
		DateTimeOffset endedAt) =>
		Build(request, jobType, BggWriteRequestStatus.Sent, collectionItemId, result, null, attempts, startedAt, endedAt);

	public static BggWriteCompletion Failed(
		BggWriteRequest request,
		string jobType,
		string error,
		int attempts,
		DateTimeOffset startedAt,
		DateTimeOffset endedAt) =>
		Build(request, jobType, BggWriteRequestStatus.Failed, null, null, error, attempts, startedAt, endedAt);

	public static BggWriteCompletion Cancelled(
		BggWriteRequest request,
		string jobType,
		string result,
		int attempts,
		DateTimeOffset startedAt,
		DateTimeOffset endedAt) =>
		Build(request, jobType, BggWriteRequestStatus.Cancelled, null, result, null, attempts, startedAt, endedAt);

	private static BggWriteCompletion Build(
		BggWriteRequest request,
		string jobType,
		BggWriteRequestStatus status,
		long? collectionItemId,
		string? result,
		string? error,
		int attempts,
		DateTimeOffset startedAt,
		DateTimeOffset endedAt)
	{
		var itemId = collectionItemId ?? request.CollectionItemId;
		var payload = JsonSerializer.Serialize(new
		{
			requestId = request.Id,
			kind = request.Kind.ToString().ToLowerInvariant(),
			bggId = request.BggId,
			gameId = request.GameId,
			name = request.Name,
			collectionItemId = itemId,
			account = request.Account
		});

		return new BggWriteCompletion(
			request.Id,
			status,
			collectionItemId,
			jobType,
			request.Account,
			payload,
			result,
			error,
			attempts,
			startedAt,
			endedAt);
	}
}