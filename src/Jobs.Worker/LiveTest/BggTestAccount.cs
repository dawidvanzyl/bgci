using BggIntegration.Domain.Models;
using Jobs.Application.Abstractions;

namespace Jobs.Worker.LiveTest;

public sealed class BggTestAccount : IBggTestAccount
{
	private const string _mask = "***";

	private readonly BggSettings _owner;
	private readonly BggSettings _test;
	private readonly BggTestAccountAllowlist _allowlist;
	private readonly string[] _secrets;

	public BggTestAccount(BggSettings owner, BggSettings test, BggTestAccountAllowlist allowlist)
	{
		_owner = owner;
		_test = test;
		_allowlist = allowlist;
		_secrets = new[] { test.Password, test.BearerToken, owner.Password, owner.BearerToken }
			.SelectMany(secret => new[] { secret, secret.Trim() })
			.Where(secret => !string.IsNullOrEmpty(secret))
			.Distinct(StringComparer.Ordinal)
			.OrderByDescending(secret => secret.Length)
			.ToArray();
	}

	public string? RefusalReason()
	{
		var missing = new List<string>();
		if (string.IsNullOrWhiteSpace(_test.Username))
		{
			missing.Add("BGG_TEST_USERNAME");
		}

		if (string.IsNullOrWhiteSpace(_test.Password))
		{
			missing.Add("BGG_TEST_PASSWORD");
		}

		if (string.IsNullOrWhiteSpace(_test.BearerToken))
		{
			missing.Add("BGG_TEST_BEARER_TOKEN");
		}

		return missing.Count > 0
			? $"these settings are missing: {string.Join(", ", missing)}."
			: !_allowlist.Contains(_test.Username)
			? $"the BGG username '{_test.Username}' is not in {BggTestAccountAllowlist.FileName}."
			: string.Equals(_test.Username.Trim(), _owner.Username.Trim(), StringComparison.OrdinalIgnoreCase)
			? "the BGG test account is the same account as the owner account."
			: null;
	}

	public string Redact(string text)
	{
		foreach (var secret in _secrets)
		{
			text = text.Replace(secret, _mask, StringComparison.Ordinal);
		}

		return text;
	}
}