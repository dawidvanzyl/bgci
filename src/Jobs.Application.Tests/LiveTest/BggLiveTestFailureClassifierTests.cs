using Jobs.Application.LiveTest;
using Shouldly;
using Xunit;

namespace Jobs.Application.Tests.LiveTest;

public sealed class BggLiveTestFailureClassifierTests
{
	[Fact]
	public void ClassifyJobError_ChallengeDuringAdd_IsChallengeAtAdd()
	{
		var failure = BggLiveTestFailureClassifier.ClassifyJobError("BGG Cloudflare challenge during add.", BggLiveTestSteps.Add);

		failure.Step.ShouldBe(BggLiveTestSteps.Add);
		failure.Kind.ShouldBe(BggLiveTestFailureKind.Challenge);
	}

	[Fact]
	public void ClassifyJobError_Status503_IsOutage()
	{
		var failure = BggLiveTestFailureClassifier.ClassifyJobError("BGG add failed with status 503: unavailable", BggLiveTestSteps.Add);

		failure.Kind.ShouldBe(BggLiveTestFailureKind.Outage);
	}

	[Fact]
	public void ClassifyJobError_RequestFailed_IsOutage()
	{
		var failure = BggLiveTestFailureClassifier.ClassifyJobError("BGG remove request failed.", BggLiveTestSteps.Remove);

		failure.Kind.ShouldBe(BggLiveTestFailureKind.Outage);
	}

	[Fact]
	public void ClassifyJobError_HttpClientTimeout_IsOutage()
	{
		var failure = BggLiveTestFailureClassifier.ClassifyJobError(
			"The request was canceled due to the configured HttpClient.Timeout of 30 seconds elapsing.",
			BggLiveTestSteps.Add);

		failure.Kind.ShouldBe(BggLiveTestFailureKind.Outage);
	}

	[Fact]
	public void ClassifyJobError_LoginStatus401_IsLoginRejectedAtLogin()
	{
		var failure = BggLiveTestFailureClassifier.ClassifyJobError("BGG login failed with status 401: bad credentials", BggLiveTestSteps.Add);

		failure.Step.ShouldBe(BggLiveTestSteps.Login);
		failure.Kind.ShouldBe(BggLiveTestFailureKind.LoginRejected);
	}

	[Theory]
	[InlineData("BGG login request failed.")]
	[InlineData("BGG login failed with status 503: unavailable")]
	public void ClassifyJobError_LoginRequestFailed_IsOutageNotLoginRejected(string error)
	{
		var failure = BggLiveTestFailureClassifier.ClassifyJobError(error, BggLiveTestSteps.Add);

		failure.Kind.ShouldBe(BggLiveTestFailureKind.Outage);
	}

	[Fact]
	public void ClassifyJobError_AddRefused_IsWriteRefusedAtAdd()
	{
		var failure = BggLiveTestFailureClassifier.ClassifyJobError("BGG add failed with status 400: refused", BggLiveTestSteps.Add);

		failure.Step.ShouldBe(BggLiveTestSteps.Add);
		failure.Kind.ShouldBe(BggLiveTestFailureKind.WriteRefused);
	}

	[Fact]
	public void ClassifyJobError_LookupInAddPhase_IsLookupFailedAtLookup()
	{
		var failure = BggLiveTestFailureClassifier.ClassifyJobError(
			"BGG collection item id lookup failed with status 404: missing",
			BggLiveTestSteps.Add);

		failure.Step.ShouldBe(BggLiveTestSteps.Lookup);
		failure.Kind.ShouldBe(BggLiveTestFailureKind.LookupFailed);
	}

	[Fact]
	public void ClassifyJobError_LookupInRemovePhase_IsAtRemove()
	{
		var failure = BggLiveTestFailureClassifier.ClassifyJobError(
			"BGG collection item id lookup failed with status 404: missing",
			BggLiveTestSteps.Remove);

		failure.Step.ShouldBe(BggLiveTestSteps.Remove);
	}

	[Fact]
	public void ClassifyJobError_WriteTimeLimit_IsTimeLimit()
	{
		var failure = BggLiveTestFailureClassifier.ClassifyJobError("BGG add exceeded the 60 second time limit.", BggLiveTestSteps.Add);

		failure.Kind.ShouldBe(BggLiveTestFailureKind.TimeLimit);
	}

	[Fact]
	public void ClassifyRead_ReadException_KeepsItsKind()
	{
		var exception = new BggTestReadException(BggLiveTestFailureKind.Challenge, "challenge");

		var failure = BggLiveTestFailureClassifier.ClassifyRead(exception, BggLiveTestSteps.Cleanup);

		failure.Step.ShouldBe(BggLiveTestSteps.Cleanup);
		failure.Kind.ShouldBe(BggLiveTestFailureKind.Challenge);
	}

	[Fact]
	public void ClassifyCancelled_RunLimit_IsTimeLimitAtThePhase()
	{
		var failure = BggLiveTestFailureClassifier.ClassifyCancelled(BggLiveTestSteps.Remove);

		failure.Step.ShouldBe(BggLiveTestSteps.Remove);
		failure.Kind.ShouldBe(BggLiveTestFailureKind.TimeLimit);
	}

	[Fact]
	public void ClassifyMissingRequest_IsWriteRefusedAtThePhase()
	{
		var requestId = Guid.NewGuid();

		var failure = BggLiveTestFailureClassifier.ClassifyMissingRequest(requestId, BggLiveTestSteps.Add);

		failure.Step.ShouldBe(BggLiveTestSteps.Add);
		failure.Kind.ShouldBe(BggLiveTestFailureKind.WriteRefused);
		failure.Detail.ShouldContain(requestId.ToString());
	}
}