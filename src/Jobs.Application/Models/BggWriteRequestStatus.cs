namespace Jobs.Application.Models;

public enum BggWriteRequestStatus
{
	Queued,
	Sent,
	Failed,
	Cancelled
}