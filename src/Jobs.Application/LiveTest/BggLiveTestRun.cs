namespace Jobs.Application.LiveTest;

internal sealed class BggLiveTestRun
{
	public Guid Id { get; } = Guid.NewGuid();

	public string Phase { get; set; } = BggLiveTestSteps.Cleanup;

	public string? Refusal { get; set; }

	public bool AddSent { get; set; }

	public bool ConfirmedGone { get; set; }

	public Guid? AddRequestId { get; set; }

	public List<Guid> RequestIds { get; } = [];

	public HashSet<long> Removed { get; } = [];

	public List<BggLiveTestFailure> Failures { get; } = [];

	public bool LoginFailedBeforeAdd =>
		!AddSent && Failures.Any(failure => failure.Step == BggLiveTestSteps.Login);

	public bool Failed => Refusal is not null || Failures.Count > 0;

	public string? Message => Refusal
		?? (Failures.Count == 0 ? null : string.Join(" Then: ", Failures.Select(failure => failure.Message)));
}