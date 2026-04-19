namespace BggIntegration.Domain.Models;

public record BggSettings
{
	public string BearerToken { get; init; } = string.Empty;
	public string Username { get; init; } = string.Empty;
	public int SyncIntervalHours { get; init; } = 6;
}
