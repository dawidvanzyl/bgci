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

	public static LiveFailure Classify(Exception exception, string phase, CancellationToken governingToken)
	{
		if (exception is OperationCanceledException)
		{
			return governingToken.IsCancellationRequested
				? new LiveFailure(phase, LiveFailureKind.TimeLimit, "The time limit ran out.")
				: new LiveFailure(phase, LiveFailureKind.Outage, "No answer from BGG.");
		}

		if (exception is LiveReadException read)
		{
			return new LiveFailure(phase, read.Kind, read.Message);
		}

		if (exception is BggWriteException write)
		{
			return new LiveFailure(StepFor(write, phase), KindOf(write), write.Message);
		}

		return new LiveFailure(phase, LiveFailureKind.ReadFailed, $"{exception.GetType().Name}: {exception.Message}");
	}

	private static string StepFor(BggWriteException exception, string phase)
	{
		if (exception.Step == LoginStep)
		{
			return LoginStep;
		}

		return phase == AddStep && exception.Step is AddStep or LookupStep ? exception.Step : phase;
	}

	private static LiveFailureKind KindOf(BggWriteException exception)
	{
		if (exception.Message.Contains("Cloudflare challenge", StringComparison.Ordinal))
		{
			return LiveFailureKind.Challenge;
		}

		if (exception.InnerException is HttpRequestException || ServerErrorPattern().IsMatch(exception.Message))
		{
			return LiveFailureKind.Outage;
		}

		return exception.Step switch
		{
			LoginStep => LiveFailureKind.LoginRejected,
			LookupStep => LiveFailureKind.LookupFailed,
			_ => LiveFailureKind.WriteRefused,
		};
	}
}
