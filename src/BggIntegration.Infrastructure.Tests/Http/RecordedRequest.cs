namespace BggIntegration.Infrastructure.Tests.Http;

public sealed record RecordedRequest(
	string Method,
	string Path,
	string Query,
	string Body,
	IReadOnlyDictionary<string, string> Headers,
	string? ContentType)
{
	public string Route => (Method, Path) switch
	{
		("POST", "/login/api/v1") => "login",
		("POST", "/api/collectionitem") => "add",
		("DELETE", _) when Path.StartsWith("/api/collectionitem/", StringComparison.Ordinal) => "remove",
		("GET", "/xmlapi2/collection") => "lookup",
		_ => $"{Method} {Path}",
	};

	public string? Cookie => Headers.GetValueOrDefault("Cookie");
}