using System.Text.Json;

namespace BggIntegration.Infrastructure.Tests.Http;

public sealed class LiveGameDataSet
{
	public const string FileName = "bgg-games.json";

	private readonly IReadOnlyList<LiveGameEntry> _entries;

	private LiveGameDataSet(IReadOnlyList<LiveGameEntry> entries)
	{
		_entries = entries;
	}

	public static LiveGameDataSet? Load(string directory)
	{
		var path = Path.Combine(directory, FileName);
		if (!File.Exists(path))
		{
			return null;
		}

		using var document = JsonDocument.Parse(File.ReadAllText(path));
		var entries = new List<LiveGameEntry>();
		if (document.RootElement.TryGetProperty("games", out var games) && games.ValueKind == JsonValueKind.Array)
		{
			foreach (var game in games.EnumerateArray())
			{
				if (game.TryGetProperty("id", out var id) && id.TryGetInt32(out var idValue))
				{
					var name = game.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty;
					var writable = game.TryGetProperty("liveWritable", out var flag) && flag.ValueKind == JsonValueKind.True;
					entries.Add(new LiveGameEntry(idValue, name, writable));
				}
			}
		}

		return new LiveGameDataSet(entries);
	}

	public LiveGameEntry? Find(int id) => _entries.FirstOrDefault(entry => entry.Id == id);
}