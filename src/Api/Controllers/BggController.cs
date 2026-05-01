using BggIntegration.Application;
using BggIntegration.Application.Queries;
using BggIntegration.Application.Services;
using BggIntegration.Domain.Models;
using GameCollection.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BggController : ControllerBase
{
	private const string _bggUnavailableMessage = "BGG is currently unavailable.";

	private readonly IMediator _mediator;
	private readonly IBggAvailabilityService _bggAvailability;

	public BggController(IMediator mediator, IBggAvailabilityService bggAvailability)
	{
		_mediator = mediator;
		_bggAvailability = bggAvailability;
	}

	[HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<BggSearchResult>>> Search(
        [FromQuery] string query,
        CancellationToken cancellationToken)
    {
        if (!_bggAvailability.IsAvailable)
		{
			return StatusCode(503, _bggUnavailableMessage);
		}

		if (string.IsNullOrWhiteSpace(query))
		{
			return BadRequest($"Query parameter '{nameof(query)}' is required.");
		}

		var results = await _mediator.Send(new SearchBggQuery(query), cancellationToken);
        return Ok(results);
    }

    [HttpGet("game/{bggId:int}")]
    public async Task<ActionResult<BggGameDetails>> GetGameDetails(
        int bggId,
        CancellationToken cancellationToken)
    {
        if (!_bggAvailability.IsAvailable)
		{
			return StatusCode(503, _bggUnavailableMessage);
		}

		var details = await _mediator.Send(new GetBggGameDetailsQuery(bggId), cancellationToken);
        return details is null ? NotFound() : Ok(details);
    }

    /// <summary>
    /// Returns a pre-translated AddGameFromBggCommand ready for the frontend to confirm and POST to /api/games/from-bgg.
    /// </summary>
    [HttpGet("game/{bggId:int}/preview")]
    public async Task<ActionResult<AddGameFromBggCommand>> PreviewGame(
        int bggId,
        CancellationToken cancellationToken)
    {
        if (!_bggAvailability.IsAvailable)
		{
			return StatusCode(503, _bggUnavailableMessage);
		}

		var details = await _mediator.Send(new GetBggGameDetailsQuery(bggId), cancellationToken);
        if (details is null)
		{
			return NotFound();
		}

		var command = details.ToAddGameCommand();
        return Ok(command);
    }

    [HttpGet("games/{bggId:int}/expansions")]
    public async Task<ActionResult<IReadOnlyList<BggSearchResult>>> GetExpansions(
        int bggId,
        CancellationToken cancellationToken)
    {
        if (!_bggAvailability.IsAvailable)
		{
			return StatusCode(503, _bggUnavailableMessage);
		}

		var results = await _mediator.Send(new GetBggExpansionsQuery(bggId), cancellationToken);
        return Ok(results);
    }
}
