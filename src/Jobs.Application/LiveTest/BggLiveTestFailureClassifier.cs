using System.Text.RegularExpressions;

namespace Jobs.Application.LiveTest;

public static partial class BggLiveTestFailureClassifier
{
	private const string _loginPrefix = "BGG login";
	private const string _addPrefix = "BGG add";
	private const string _lookupPrefix = "BGG collection item id lookup";

	[GeneratedRegex(@"with status 5\d\d")]
	private static partial Regex ServerErrorPattern();

	public static BggLiveTestFailure ClassifyJobError(string error, string phase) =>
		new(StepOf(error, phase), KindOf(error), error);

	public static BggLiveTestFailure ClassifyRead(BggTestReadException exception, string phase) =>
		new(phase, exception.Kind, exception.Message);

	public static BggLiveTestFailure ClassifyCancelled(string phase) =>
		new(phase, BggLiveTestFailureKind.TimeLimit, "The time limit ran out.");

	public static BggLiveTestFailure ClassifyMissingRequest(Guid requestId, string phase) =>
		new(phase, BggLiveTestFailureKind.WriteRefused, $"the request row {requestId} is missing");

	public static BggLiveTestFailure ClassifyUnexpected(Exception exception, string phase) =>
		new(phase, BggLiveTestFailureKind.ReadFailed, $"{exception.GetType().Name}: {exception.Message}");

	private static string StepOf(string error, string phase) => error switch
	{
		_ when error.StartsWith(_loginPrefix, StringComparison.Ordinal) => BggLiveTestSteps.Login,
		_ when phase == BggLiveTestSteps.Add && error.StartsWith(_addPrefix, StringComparison.Ordinal) => BggLiveTestSteps.Add,
		_ when phase == BggLiveTestSteps.Add && error.StartsWith(_lookupPrefix, StringComparison.Ordinal) => BggLiveTestSteps.Lookup,
		_ => phase
	};

	private static BggLiveTestFailureKind KindOf(string error) => error switch
	{
		_ when error.Contains("Cloudflare challenge", StringComparison.Ordinal) => BggLiveTestFailureKind.Challenge,
		_ when IsOutage(error) => BggLiveTestFailureKind.Outage,
		_ when error.Contains("exceeded the 60 second time limit", StringComparison.Ordinal) => BggLiveTestFailureKind.TimeLimit,
		_ when error.StartsWith(_loginPrefix, StringComparison.Ordinal) => BggLiveTestFailureKind.LoginRejected,
		_ when error.StartsWith(_lookupPrefix, StringComparison.Ordinal) => BggLiveTestFailureKind.LookupFailed,
		_ => BggLiveTestFailureKind.WriteRefused
	};

	private static bool IsOutage(string error) =>
		error.Contains("request failed.", StringComparison.Ordinal)
		|| error.Contains("HttpClient.Timeout", StringComparison.Ordinal)
		|| ServerErrorPattern().IsMatch(error);
}