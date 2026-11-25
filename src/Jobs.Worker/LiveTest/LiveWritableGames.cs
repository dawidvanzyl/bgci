using Jobs.Application.Abstractions;
using Jobs.Application.LiveTest;
using System.Text.Json;

namespace Jobs.Worker.LiveTest;

public sealed class LiveWritableGames : ILiveWritableGames
{
	public const string FileName = "bgg-games.json";

	private readonly IReadOnlyList<LiveTestGame> _games;

	private LiveWritableGames(IReadOnlyList<LiveTestGame> games)
	{
		_games = games;
	}

	public static LiveWritableGames Load(string directory)
	{
		var path = Path.Combine(directory, FileName);
		if (!File.Exists(path))
		{
			return new LiveWritableGames([]);
		}

		using var document = JsonDocument.Parse(File.ReadAllText(path));
		var games = new List<LiveTestGame>();
		if (document.RootElement.TryGetProperty("games", out var array) && array.ValueKind == JsonValueKind.Array)
		{
			foreach (var game in array.EnumerateArray())
			{
				if (game.TryGetProperty("id", out var id) && id.TryGetInt32(out var idValue))
				{
					var name = game.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty;
					var writable = game.TryGetProperty("liveWritable", out var flag) && flag.ValueKind == JsonValueKind.True;
					games.Add(new LiveTestGame(idValue, name, writable));
				}
			}
		}

		return new LiveWritableGames(games);
	}

	public LiveTestGame? Find(int bggId) => _games.FirstOrDefault(game => game.Id == bggId);
}