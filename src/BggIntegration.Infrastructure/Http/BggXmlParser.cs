using System.Globalization;
using System.Xml.Linq;
using BggIntegration.Domain.Models;

namespace BggIntegration.Infrastructure.Http;

public static class BggXmlParser
{
	public static IReadOnlyList<BggSearchResult> ParseSearchResults(XDocument doc)
	{
		return doc.Descendants("item")
			.Select(item =>
			{
				var id = int.Parse(item.Attribute("id")?.Value ?? "0");
				var name = item.Elements("name")
					.FirstOrDefault(n => n.Attribute("type")?.Value == "primary")
					?.Attribute("value")
					?.Value ?? string.Empty;
				var yearStr = item.Element("yearpublished")?.Attribute("value")?.Value;
				var year = int.TryParse(yearStr, out var y) ? y : (int?)null;

				return new BggSearchResult(id, name, year, null);
			})
			.Where(r => r.BggId > 0 && !string.IsNullOrEmpty(r.Name))
			.ToList()
			.AsReadOnly();
	}

	public static BggGameDetails? ParseGameDetails(XDocument doc, int bggId)
	{
		var item = doc.Descendants("item").FirstOrDefault();
		if (item is null)
		{
			return null;
		}

		var name = item
			.Elements("name")
			.FirstOrDefault(n => n.Attribute("type")?.Value == "primary")
			?.Attribute("value")
			?.Value ?? string.Empty;

		var yearStr = item.Element("yearpublished")?.Attribute("value")?.Value;
		var year = int.TryParse(yearStr, out var y) ? y : (int?)null;

		var description = System.Net.WebUtility.HtmlDecode(item.Element("description")?.Value ?? string.Empty);

		var minPlayersStr = item.Element("minplayers")?.Attribute("value")?.Value;
		var minPlayers = int.TryParse(minPlayersStr, out var minP) && minP > 0 ? minP : (int?)null;

		var maxPlayersStr = item.Element("maxplayers")?.Attribute("value")?.Value;
		var maxPlayers = int.TryParse(maxPlayersStr, out var maxP) && maxP > 0 ? maxP : (int?)null;

		var playTimeStr = item.Element("playingtime")?.Attribute("value")?.Value;
		var playTime = int.TryParse(playTimeStr, out var pt) && pt > 0 ? pt : (int?)null;

		var minPlayTimeStr = item.Element("minplaytime")?.Attribute("value")?.Value;
		var minPlayTime = int.TryParse(minPlayTimeStr, out var minPt) && minPt > 0 ? minPt : (int?)null;

		var maxPlayTimeStr = item.Element("maxplaytime")?.Attribute("value")?.Value;
		var maxPlayTime = int.TryParse(maxPlayTimeStr, out var maxPt) && maxPt > 0 ? maxPt : (int?)null;

		var minAgeStr = item.Element("minage")?.Attribute("value")?.Value;
		var minAge = int.TryParse(minAgeStr, out var ma) && ma > 0 ? ma : (int?)null;

		var ratings = item.Element("statistics")?.Element("ratings");
		var ratingStr = ratings?.Element("average")?.Attribute("value")?.Value;
		decimal? rating = decimal.TryParse(ratingStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) && r > 0 ? r : null;

		var weightStr = ratings?.Element("averageweight")?.Attribute("value")?.Value;
		decimal? weight = decimal.TryParse(weightStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var w) && w > 0 ? w : null;

		var thumbnail = item.Element("thumbnail")?.Value?.Trim();
		var image = item.Element("image")?.Value?.Trim();

		var categories = item.Elements("link")
			.Where(l => l.Attribute("type")?.Value == "boardgamecategory")
			.Select(l => l.Attribute("value")?.Value ?? string.Empty)
			.Where(v => !string.IsNullOrEmpty(v))
			.ToList();

		var mechanics = item.Elements("link")
			.Where(l => l.Attribute("type")?.Value == "boardgamemechanic")
			.Select(l => l.Attribute("value")?.Value ?? string.Empty)
			.Where(v => !string.IsNullOrEmpty(v))
			.ToList();

		var designers = item.Elements("link")
			.Where(l => l.Attribute("type")?.Value == "boardgamedesigner")
			.Select(l => l.Attribute("value")?.Value ?? string.Empty)
			.Where(v => !string.IsNullOrEmpty(v))
			.ToList();

		var artists = item.Elements("link")
			.Where(l => l.Attribute("type")?.Value == "boardgameartist")
			.Select(l => l.Attribute("value")?.Value ?? string.Empty)
			.Where(v => !string.IsNullOrEmpty(v))
			.ToList();

		var publishers = item.Elements("link")
			.Where(l => l.Attribute("type")?.Value == "boardgamepublisher")
			.Select(l => l.Attribute("value")?.Value ?? string.Empty)
			.Where(v => !string.IsNullOrEmpty(v))
			.ToList();

		var subdomains = item.Elements("link")
			.Where(l => l.Attribute("type")?.Value == "boardgamesubdomain")
			.Select(l => l.Attribute("value")?.Value ?? string.Empty)
			.Where(v => !string.IsNullOrEmpty(v))
			.ToList();

		// Derive best player count from the suggested_numplayers community poll
		var poll = item.Elements("poll")
			.FirstOrDefault(p => p.Attribute("name")?.Value == "suggested_numplayers");

		int? bestPlayerCountMin = null;
		int? bestPlayerCountMax = null;

		if (poll is not null &&
		    int.TryParse(poll.Attribute("totalvotes")?.Value, out var totalVotes) && totalVotes >= 10)
		{
			var entries = poll.Elements("results")
				.Select(r =>
				{
					var raw    = r.Attribute("numplayers")?.Value ?? "";
					var isPlus = raw.EndsWith("+");
					var numStr = isPlus ? raw.TrimEnd('+') : raw;
					if (!int.TryParse(numStr, out var count) || count <= 0) return null;
					var bestVotes = int.TryParse(
						r.Elements("result")
						 .FirstOrDefault(x => x.Attribute("value")?.Value == "Best")
						 ?.Attribute("numvotes")?.Value,
						out var b) ? b : 0;
					return new { Count = count, IsPlus = isPlus, Best = bestVotes };
				})
				.Where(x => x != null)
				.ToList();

			if (entries.Count > 0)
			{
				var topBest = entries.Max(x => x!.Best);
				if (topBest > 0)
				{
					var winners   = entries.Where(x => x!.Best == topBest).ToList();
					var minWinner = winners.OrderBy(x => x!.Count).First()!;
					var maxWinner = winners.OrderByDescending(x => x!.Count).First()!;
					bestPlayerCountMin = minWinner.Count;
					bestPlayerCountMax = (winners.Count > 1 || maxWinner.IsPlus)
						? (maxWinner.IsPlus ? 99 : maxWinner.Count)
						: (int?)null;
				}
			}
		}

		// Inbound boardgameexpansion links identify base games this item is an expansion of
		var parentBggIds = item.Elements("link")
			.Where(l => l.Attribute("type")?.Value == "boardgameexpansion"
			         && l.Attribute("inbound")?.Value == "true")
			.Select(l => int.TryParse(l.Attribute("id")?.Value, out var pid) ? pid : 0)
			.Where(pid => pid > 0)
			.ToList()
			.AsReadOnly();

		return new BggGameDetails(
			BggId: bggId,
			Name: name,
			Year: year,
			Description: description,
			MinPlayers: minPlayers,
			MaxPlayers: maxPlayers,
			PlayTimeMinutes: playTime,
			MinPlayTimeMinutes: minPlayTime,
			MaxPlayTimeMinutes: maxPlayTime,
			AverageRating: rating,
			AverageWeight: weight,
			MinAge: minAge,
			BestPlayerCountMin: bestPlayerCountMin,
			BestPlayerCountMax: bestPlayerCountMax,
			ThumbnailUrl: string.IsNullOrEmpty(thumbnail) ? null : thumbnail,
			ImageUrl: string.IsNullOrEmpty(image) ? null : image,
			Categories: categories,
			Mechanics: mechanics,
			Designers: designers,
			Artists: artists,
			Publishers: publishers,
			Subdomains: subdomains,
			ParentBggIds: parentBggIds
		);
	}

	/// <summary>
	/// Parses known expansions for a base game from a <c>thing</c> XML response.
	/// Returns non-inbound <c>boardgameexpansion</c> link elements (child expansions, not parent games).
	/// </summary>
	public static IReadOnlyList<BggSearchResult> ParseExpansionLinks(XDocument doc)
	{
		var item = doc.Descendants("item").FirstOrDefault();
		if (item is null)
		{
			return Array.Empty<BggSearchResult>();
		}

		return item.Elements("link")
			.Where(l => l.Attribute("type")?.Value == "boardgameexpansion"
			         && l.Attribute("inbound") is null)
			.Select(l =>
			{
				var id   = int.TryParse(l.Attribute("id")?.Value, out var eid) ? eid : 0;
				var name = l.Attribute("value")?.Value ?? string.Empty;
				return new BggSearchResult(id, name, null, null);
			})
			.Where(r => r.BggId > 0 && !string.IsNullOrEmpty(r.Name))
			.ToList()
			.AsReadOnly();
	}

	public static IReadOnlyList<BggCollectionItem> ParseCollection(XDocument doc)
	{
		return doc.Descendants("item")
			.Where(item => item.Element("status")?.Attribute("own")?.Value == "1")
			.Select(item =>
			{
				var bggId = int.Parse(item.Attribute("objectid")?.Value ?? "0");
				var collId = long.TryParse(item.Attribute("collid")?.Value, out var cid) ? cid : 0L;
				var name = item.Element("name")?.Value?.Trim() ?? string.Empty;
				var yearStr = item.Element("yearpublished")?.Value?.Trim();
				var year = int.TryParse(yearStr, out var y) ? y : (int?)null;
				var thumbnail = item.Element("thumbnail")?.Value?.Trim();

				return new BggCollectionItem(
					BggId: bggId,
					CollId: collId,
					Name: name,
					Year: year,
					ThumbnailUrl: string.IsNullOrEmpty(thumbnail) ? null : thumbnail
				);
			})
			.Where(c => c.BggId > 0 && !string.IsNullOrEmpty(c.Name))
			.ToList()
			.AsReadOnly();
	}
}
