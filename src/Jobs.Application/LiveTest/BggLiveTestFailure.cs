namespace Jobs.Application.LiveTest;

public sealed record BggLiveTestFailure(string Step, BggLiveTestFailureKind Kind, string Detail)
{
	public string Message => $"BGG live write failed at step '{Step}': {Kind}. {Detail}";
}