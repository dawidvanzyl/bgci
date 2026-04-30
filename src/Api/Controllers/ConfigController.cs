using BggIntegration.Application.Services;
using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
	private readonly BggSettings _bggSettings;
	private readonly BggWriterSettings _bggWriterSettings;
	private readonly IBggAvailabilityService _bggAvailability;

	public ConfigController(
		IOptions<BggSettings> bggSettings,
		IOptions<BggWriterSettings> bggWriterSettings,
		IBggAvailabilityService bggAvailability)
	{
		_bggSettings = bggSettings.Value;
		_bggWriterSettings = bggWriterSettings.Value;
		_bggAvailability = bggAvailability;
	}

	[HttpGet]
	public IActionResult Get()
	{
		// Search requires the writer sidecar because search results are only surfaced to be added.
		// Without the writer, add would be unavailable, making search a dead-end.
		// A bearer token alone is not sufficient.
		var bggConfigured = !string.IsNullOrWhiteSpace(_bggSettings.BearerToken) && !string.IsNullOrWhiteSpace(_bggWriterSettings.BaseUrl);
		var bggReachable = _bggAvailability.IsAvailable;

		var infoVersion = Assembly.GetEntryAssembly()
			?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
			?.InformationalVersion;
		// Strip build metadata (e.g. "+abcdef"); return "dev" for non-release builds
		var rawVersion = infoVersion?.Split('+')[0];
		var version = rawVersion is null || rawVersion.Contains('-') ? "dev" : rawVersion;

		return Ok(new
		{
			version,
			bggConfigured,
			bggReachable,
			bggSearchEnabled  = bggConfigured && bggReachable,
			bggCollectionEnabled = bggConfigured && bggReachable && !string.IsNullOrWhiteSpace(_bggSettings.Username),

		// bggSyncEnabled depends only on configuration — reachability controls the disabled state in the frontend
		bggSyncEnabled = bggConfigured && !string.IsNullOrWhiteSpace(_bggSettings.Username),
		});
	}
}
