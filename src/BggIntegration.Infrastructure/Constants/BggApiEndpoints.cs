namespace BggIntegration.Infrastructure.Constants;

internal static class BggApiEndpoints
{
	internal const string Search = "xmlapi2/search?query={0}&type=boardgame";
	internal const string GameDetail = "xmlapi2/thing?id={0}&stats=1";
	internal const string Collection = "xmlapi2/collection?username={0}&own=1&excludesubtype=boardgameexpansion";
	internal const string ExpansionCollection = "xmlapi2/collection?username={0}&own=1&subtype=boardgameexpansion";
	internal const string Login = "login/api/v1";
	internal const string CollectionItem = "api/collectionitem";
	internal const string CollectionItemById = "api/collectionitem/{0}";
	internal const string CollectionItemLookup = "xmlapi2/collection?username={0}&id={1}&own=1";
}