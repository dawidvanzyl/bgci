namespace Jobs.Application.Models;

public sealed record BggWriteRequest(
	Guid Id,
	BggWriteKind Kind,
	int BggId,
	Guid GameId,
	string Name,
	long? CollectionItemId,
	string Account,
	BggWriteRequestStatus Status,
	int Attempts);