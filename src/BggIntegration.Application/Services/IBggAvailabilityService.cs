namespace BggIntegration.Application.Services;

public interface IBggAvailabilityService
{
	/// <summary>
	/// True if the most recent probe confirmed BGG is reachable.
	/// Defaults to false (Unknown) until the first probe completes.
	/// </summary>
	bool IsAvailable { get; }

	/// <summary>
	/// UTC timestamp of the last completed probe. Null until the first probe runs.
	/// </summary>
	DateTime? LastChecked { get; }

	/// <summary>
	/// Probes BGG to determine current reachability. Updates IsAvailable and LastChecked.
	/// Returns true if BGG was previously unavailable and is now available (i.e. a restore occurred).
	/// </summary>
	Task<bool> ProbeAsync(CancellationToken cancellationToken);
}
