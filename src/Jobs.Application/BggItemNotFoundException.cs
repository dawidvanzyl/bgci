namespace Jobs.Application;

public sealed class BggItemNotFoundException : Exception
{
	public BggItemNotFoundException(int bggId)
		: base($"BGG has no collection item for bggId {bggId}.")
	{
		BggId = bggId;
	}

	public int BggId { get; }
}