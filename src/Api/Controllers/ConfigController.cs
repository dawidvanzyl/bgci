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
		var bggSearchEnabled = !string.IsNullOrWhiteSpace(_bggSettings.BearerToken) && !string.IsNullOrWhiteSpace(_bggWriterSettings.BaseUrl);

		return Ok(new
		{
			bggSearchEnabled,
			bggCollectionEnabled = bggSearchEnabled && !string.IsNullOrWhiteSpace(_bggSettings.Username)
		});
	}
}
