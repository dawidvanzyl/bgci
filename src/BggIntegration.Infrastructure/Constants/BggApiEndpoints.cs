namespace BggIntegration.Infrastructure.Constants;

internal static class BggApiEndpoints
{
	internal const string Search     = "search?query={0}&type=boardgame";
	internal const string GameDetail = "thing?id={0}&stats=1";
	internal const string Collection = "collection?username={0}&own=1&excludesubtype=boardgameexpansion";
}
