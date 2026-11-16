using System.Text.Json;

namespace BggIntegration.Infrastructure.Tests.Http;

public sealed class LiveTestAllowlist
{
	public const string FileName = "bgg-live-test-accounts.json";

	private readonly IReadOnlyList<string> _usernames;

	private LiveTestAllowlist(IReadOnlyList<string> usernames)
	{
		_usernames = usernames;
	}

	public int Count => _usernames.Count;

	public static LiveTestAllowlist? Load(string directory)
	{
		var path = Path.Combine(directory, FileName);
		if (!File.Exists(path))
		{
			return null;
		}

		using var document = JsonDocument.Parse(File.ReadAllText(path));
		var usernames = new List<string>();
		if (document.RootElement.TryGetProperty("usernames", out var array) && array.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in array.EnumerateArray())
			{
				var username = item.ValueKind == JsonValueKind.String ? item.GetString()?.Trim() : null;
				if (!string.IsNullOrEmpty(username))
				{
					usernames.Add(username);
				}
			}
		}

		return new LiveTestAllowlist(usernames);
	}

	public bool Contains(string username) =>
		_usernames.Any(allowed => string.Equals(allowed, username.Trim(), StringComparison.OrdinalIgnoreCase));
}
