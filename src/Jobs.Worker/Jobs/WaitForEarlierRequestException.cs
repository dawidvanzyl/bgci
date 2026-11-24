namespace Jobs.Worker.Jobs;

internal sealed class WaitForEarlierRequestException : Exception
{
	public WaitForEarlierRequestException(Guid requestId)
		: base($"BGG write request {requestId} waits for an earlier request for the same game.")
	{
	}
}
