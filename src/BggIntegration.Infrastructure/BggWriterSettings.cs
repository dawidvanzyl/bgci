namespace BggIntegration.Infrastructure;

public record BggWriterSettings
{
	public string BaseUrl { get; init; } = string.Empty;
}
