namespace GameCollection.Application;

public sealed class BggRemoveNotFinishedException : Exception
{
	public BggRemoveNotFinishedException(int bggId)
		: base($"A remove of BGG game {bggId} is not finished.")
	{
		BggId = bggId;
	}

	public int BggId { get; }
}