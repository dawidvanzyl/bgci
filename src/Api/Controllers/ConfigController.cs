using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
	private readonly IConfiguration _configuration;

	public ConfigController(IConfiguration configuration) => _configuration = configuration;

	// GET /api/config
	[HttpGet]
	public IActionResult Get()
	{
		var bearerToken = _configuration["Bgg:BearerToken"];
		return Ok(new
		{
			bggSearchEnabled = !string.IsNullOrWhiteSpace(bearerToken)
		});
	}
}
