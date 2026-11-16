using BggIntegration.Application.Services;
using BggIntegration.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
	private readonly BggSettings _bggSettings;
	private readonly IBggAvailabilityService _bggAvailability;

	public ConfigController(
		IOptions<BggSettings> bggSettings,
		IBggAvailabilityService bggAvailability)
	{
		_bggSettings = bggSettings.Value;
		_bggAvailability = bggAvailability;
	}

	[HttpGet]
	public IActionResult Get()
	{
		// Search results exist only to be added, and an add needs the password.
		var bggConfigured = !string.IsNullOrWhiteSpace(_bggSettings.BearerToken) && !string.IsNullOrWhiteSpace(_bggSettings.Password);
		var bggReachable = _bggAvailability.IsAvailable;

		var raw = Environment.GetEnvironmentVariable("APP_VERSION")?.Trim();
		var version = string.IsNullOrWhiteSpace(raw) ? "dev" : raw.TrimStart('v');

		return Ok(new
		{
			version,
			bggConfigured,
			bggReachable,
			bggSearchEnabled = bggConfigured && bggReachable,
			bggCollectionEnabled = bggConfigured && bggReachable && !string.IsNullOrWhiteSpace(_bggSettings.Username),

			// bggSyncEnabled depends only on configuration — reachability controls the disabled state in the frontend
			bggSyncEnabled = bggConfigured && !string.IsNullOrWhiteSpace(_bggSettings.Username),
		});
	}
}