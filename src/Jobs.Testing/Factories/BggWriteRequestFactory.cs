using Jobs.Application.Models;

namespace Jobs.Testing.Factories;

public static class BggWriteRequestFactory
{
	public const int DefaultBggId = 13;

	public static BggWriteRequest CreateAdd(
		Guid? id = null,
		Guid? gameId = null,
		int bggId = DefaultBggId,
		string name = "Catan",
		BggWriteRequestStatus status = BggWriteRequestStatus.Queued,
		int attempts = 0)
	{
		return new BggWriteRequest(
			id ?? Guid.NewGuid(),
			BggWriteKind.Add,
			bggId,
			gameId ?? Guid.NewGuid(),
			name,
			null,
			"owner",
			status,
			attempts);
	}

	public static BggWriteRequest CreateRemove(
		Guid? id = null,
		Guid? gameId = null,
		int bggId = DefaultBggId,
		long? collectionItemId = null,
		BggWriteRequestStatus status = BggWriteRequestStatus.Queued,
		int attempts = 0)
	{
		return new BggWriteRequest(
			id ?? Guid.NewGuid(),
			BggWriteKind.Remove,
			bggId,
			gameId ?? Guid.NewGuid(),
			string.Empty,
			collectionItemId,
			"owner",
			status,
			attempts);
	}
}