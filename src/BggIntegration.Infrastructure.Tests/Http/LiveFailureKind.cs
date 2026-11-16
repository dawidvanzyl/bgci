namespace BggIntegration.Infrastructure.Tests.Http;

public enum LiveFailureKind
{
	Challenge,
	Outage,
	LoginRejected,
	WriteRefused,
	LookupFailed,
	ReadFailed,
	StillListed,
	TimeLimit,
}
