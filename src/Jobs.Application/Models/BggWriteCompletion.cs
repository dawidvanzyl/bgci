namespace Jobs.Application.Models;

public sealed record BggWriteCompletion(
	Guid RequestId,
	BggWriteRequestStatus Status,
	long? CollectionItemId,
	string JobType,
	string Account,
	string Payload,
	string? Result,
	string? Error,
	int Attempts,
	DateTimeOffset StartedAt,
	DateTimeOffset EndedAt);
