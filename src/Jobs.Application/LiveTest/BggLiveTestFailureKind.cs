namespace Jobs.Application.LiveTest;

public enum BggLiveTestFailureKind
{
	Challenge,
	Outage,
	LoginRejected,
	WriteRefused,
	LookupFailed,
	ReadFailed,
	StillListed,
	TimeLimit
}