using BggIntegration.Infrastructure.Http;
using System.Text.RegularExpressions;

namespace BggIntegration.Infrastructure.Tests.Http;

public static partial class LiveFailureClassifier
{
	public const string LoginStep = "login";
	public const string AddStep = "add";
	public const string LookupStep = "collection item id lookup";
	public const string RemoveStep = "remove";
	public const string CleanupStep = "cleanup";

	[GeneratedRegex(@"with status 5\d\d")]
	private static partial Regex ServerErrorPattern();

	public static LiveFailure Classify(Exception exception, string phase, CancellationToken governingToken, string? timedOutStep = null) => exception switch
	{
		OperationCanceledException when governingToken.IsCancellationRequested =>
			new LiveFailure(phase, LiveFailureKind.TimeLimit, "The time limit ran out."),
		OperationCanceledException => new LiveFailure(timedOutStep ?? phase, LiveFailureKind.Outage, "No answer from BGG."),
		LiveReadException read => new LiveFailure(phase, read.Kind, read.Message),
		BggWriteException write => new LiveFailure(StepFor(write, phase), KindOf(write), write.Message),
		_ => new LiveFailure(phase, LiveFailureKind.ReadFailed, $"{exception.GetType().Name}: {exception.Message}"),
	};

	private static string StepFor(BggWriteException exception, string phase) =>
		exception.Step == LoginStep || (phase == AddStep && exception.Step is AddStep or LookupStep)
			? exception.Step
			: phase;

	private static LiveFailureKind KindOf(BggWriteException exception) => exception switch
	{
		_ when exception.Message.Contains("Cloudflare challenge", StringComparison.Ordinal) => LiveFailureKind.Challenge,
		_ when exception.InnerException is HttpRequestException || ServerErrorPattern().IsMatch(exception.Message) => LiveFailureKind.Outage,
		{ Step: LoginStep } => LiveFailureKind.LoginRejected,
		{ Step: LookupStep } => LiveFailureKind.LookupFailed,
		_ => LiveFailureKind.WriteRefused,
	};
}