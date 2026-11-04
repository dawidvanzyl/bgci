using GameCollection.Domain.Aggregates;
using GameCollection.Domain.ValueObjects;

namespace GameCollection.Testing.Factories;

/// <summary>
/// Builds valid <see cref="CollectedGame"/> aggregates. Tests state only the values that matter
/// to the behaviour under test.
/// </summary>
public static class CollectedGameFactory
{
	public const int DefaultBggId = 13;
	public const long DefaultBggCollId = 99;

	public static CollectedGame CreateManual(string name = "Catan")
	{
		return CollectedGame.Create(new GameName(name));
	}

	public static CollectedGame CreateFromBgg(
		string name = "Catan",
		int bggId = DefaultBggId,
		long? bggCollId = DefaultBggCollId)
	{
		return CollectedGame.Create(new GameName(name), bggId: new BggGameId(bggId), bggCollId: bggCollId);
	}
}