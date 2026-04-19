using BggIntegration.Domain.Models;
using BggIntegration.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
	private readonly BggSettings _bggSettings;
	private readonly BggWriterSettings _bggWriterSettings;

	public ConfigController(IOptions<BggSettings> bggSettings, IOptions<BggWriterSettings> bggWriterSettings)
	{
		_bggSettings = bggSettings.Value;
		_bggWriterSettings = bggWriterSettings.Value;
	}

	[HttpGet]
	public IActionResult Get()
	{
		// Search requires the writer sidecar because search results are only surfaced to be added.
		// Without the writer, add would be unavailable, making search a dead-end.
		// A bearer token alone is not sufficient.
		var bggSearchEnabled = !string.IsNullOrWhiteSpace(_bggSettings.BearerToken) && !string.IsNullOrWhiteSpace(_bggWriterSettings.BaseUrl);

		return Ok(new
		{
			bggSearchEnabled,
			bggCollectionEnabled = bggSearchEnabled && !string.IsNullOrWhiteSpace(_bggSettings.Username)
		});
	}
}
