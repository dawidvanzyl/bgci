using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure.Http;
using Jobs.Infrastructure.Jobs;
using Jobs.Worker.LiveTest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobs.Worker.Bgg;

public sealed class BggAccountClients : IBggAccountClients
{
	public const string BggWriteClientName = "bgg-write";

	private readonly IHttpClientFactory _httpClientFactory;
	private readonly TimeProvider _timeProvider;
	private readonly ILogger<BggWriteHttpClient> _logger;
	private readonly BggTestAccountAllowlist _allowlist;
	private readonly BggSettings _ownerSettings;
	private readonly BggSettings _testSettings;
	private readonly BggSession _ownerSession = new();
	private readonly BggSession _testSession = new();

	public BggAccountClients(
		IHttpClientFactory httpClientFactory,
		TimeProvider timeProvider,
		ILogger<BggWriteHttpClient> logger,
		BggTestAccountAllowlist allowlist,
		BggSettings ownerSettings,
		BggSettings testSettings)
	{
		_httpClientFactory = httpClientFactory;
		_timeProvider = timeProvider;
		_logger = logger;
		_allowlist = allowlist;
		_ownerSettings = ownerSettings;
		_testSettings = testSettings;
	}

	public BggAccountClient Get(string account) => account switch
	{
		BggAccounts.Owner => Create(_ownerSettings, _ownerSession),
		BggAccounts.Test => Create(RequireAllowedTestAccount(), _testSession),
		_ => throw new BggWriteException("configuration", $"Unknown BGG account '{account}'.")
	};

	private BggSettings RequireAllowedTestAccount()
	{
		return string.IsNullOrWhiteSpace(_testSettings.Username)
			? throw new BggWriteException("configuration", "The BGG test account username is not configured.")
			: !_allowlist.Contains(_testSettings.Username)
			? throw new BggWriteException("configuration", "The BGG test account username is not on the allowlist.")
			: string.Equals(_testSettings.Username.Trim(), _ownerSettings.Username.Trim(), StringComparison.OrdinalIgnoreCase)
			? throw new BggWriteException("configuration", "The BGG test account username is the same as the owner username.")
			: _testSettings;
	}

	private BggAccountClient Create(BggSettings settings, BggSession session)
	{
		var client = new BggWriteHttpClient(
			_httpClientFactory.CreateClient(BggWriteClientName),
			session,
			Options.Create(settings),
			_timeProvider,
			_logger);
		return new BggAccountClient(client, settings);
	}
}