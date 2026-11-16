namespace BggIntegration.Infrastructure.Tests.Http;

public sealed record LiveBggSettings(string Username, string Password, string BearerToken)
{
	public const string UsernameVariable = "BGG_LIVE_USERNAME";
	public const string PasswordVariable = "BGG_LIVE_PASSWORD";
	public const string BearerTokenVariable = "BGG_LIVE_BEARER_TOKEN";

	public static LiveBggSettings FromEnvironment() => new(
		Environment.GetEnvironmentVariable(UsernameVariable) ?? string.Empty,
		Environment.GetEnvironmentVariable(PasswordVariable) ?? string.Empty,
		Environment.GetEnvironmentVariable(BearerTokenVariable) ?? string.Empty);

	public IReadOnlyList<string> MissingVariables()
	{
		var missing = new List<string>();
		if (string.IsNullOrWhiteSpace(Username))
		{
			missing.Add(UsernameVariable);
		}

		if (string.IsNullOrWhiteSpace(Password))
		{
			missing.Add(PasswordVariable);
		}

		if (string.IsNullOrWhiteSpace(BearerToken))
		{
			missing.Add(BearerTokenVariable);
		}

		return missing;
	}
}
