namespace BggIntegration.Infrastructure.Tests.Http;

public sealed class SecretRedactor
{
	private const string Mask = "***";

	private readonly string[] _secrets;

	public SecretRedactor(params string[] secrets)
	{
		_secrets = secrets
			.SelectMany(secret => new[] { secret, secret.Trim() })
			.Where(secret => !string.IsNullOrEmpty(secret))
			.Distinct(StringComparer.Ordinal)
			.OrderByDescending(secret => secret.Length)
			.ToArray();
	}

	public string Redact(string text)
	{
		foreach (var secret in _secrets)
		{
			text = text.Replace(secret, Mask, StringComparison.Ordinal);
		}

		return text;
	}
}
