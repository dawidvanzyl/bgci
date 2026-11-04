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

	public static CollectedGame CreateDetailed(
		string name = "Catan",
		IEnumerable<string>? categories = null,
		IEnumerable<string>? mechanics = null,
		IEnumerable<string>? designers = null,
		IEnumerable<string>? artists = null,
		IEnumerable<string>? publishers = null,
		IEnumerable<string>? subdomains = null,
		int? bggId = null,
		long? bggCollId = null,
		GameId? parentGameId = null,
		decimal? bggRating = null,
		decimal? bggWeight = null,
		int? minAge = null,
		int? minPlayers = null,
		int? maxPlayers = null,
		int? playTimeMinutes = null,
		int? minPlayTimeMinutes = null,
		int? maxPlayTimeMinutes = null,
		int? bestPlayerCountMin = null,
		int? bestPlayerCountMax = null)
	{
		return CollectedGame.Create(
			new GameName(name),
			year: 2019,
			description: "A detailed game",
			playerCount: minPlayers is not null && maxPlayers is not null ? new PlayerCount(minPlayers.Value, maxPlayers.Value) : null,
			playTime: playTimeMinutes is not null ? new PlayTime(playTimeMinutes.Value) : null,
			minPlayTimeMinutes: minPlayTimeMinutes,
			maxPlayTimeMinutes: maxPlayTimeMinutes,
			bggRating: bggRating is not null ? new BggRating(bggRating.Value) : null,
			bggWeight: bggWeight is not null ? new BggWeight(bggWeight.Value) : null,
			minAge: minAge,
			bestPlayerCountMin: bestPlayerCountMin,
			bestPlayerCountMax: bestPlayerCountMax,
			coverImageUrl: new Uri("https://example.test/cover.png"),
			categories: categories,
			mechanics: mechanics,
			designers: designers,
			artists: artists,
			publishers: publishers,
			subdomains: subdomains,
			bggId: bggId is not null ? new BggGameId(bggId.Value) : null,
			bggCollId: bggCollId,
			parentGameId: parentGameId);
	}

	public static CollectedGame CreateFromBgg(
		string name = "Catan",
		int bggId = DefaultBggId,
		long? bggCollId = DefaultBggCollId)
	{
		return CollectedGame.Create(new GameName(name), bggId: new BggGameId(bggId), bggCollId: bggCollId);
	}
}