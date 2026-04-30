using BggIntegration.Application.Services;
using BggIntegration.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
	private readonly BggSyncService _syncService;
	private readonly BggSettings _bggSettings;

	public SyncController(BggSyncService syncService, IOptions<BggSettings> bggSettings)
	{
		_syncService = syncService;
		_bggSettings = bggSettings.Value;
	}

	[HttpPost("bgg")]
	public async Task<IActionResult> SyncBgg(CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(_bggSettings.Username))
			return BadRequest("BGG username is not configured.");

		await _syncService.SyncAsync(_bggSettings.Username, cancellationToken);
		return NoContent();
	}
}
