namespace Jobs.Application.LiveTest;

public sealed record BggLiveTestRecord(
	string JobType,
	string Account,
	string Payload,
	string? Result,
	string? Error,
	DateTimeOffset StartedAt,
	DateTimeOffset EndedAt);