namespace Api.Responses;

public sealed record AddGameFromBggRefusal(string Reason)
{
	public const string BggRemoveNotFinished = "bgg-remove-not-finished";
}
