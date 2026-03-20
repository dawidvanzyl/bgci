using BggIntegration.Application;
using BggIntegration.Application.Queries;
using BggIntegration.Domain.Models;
using GameCollection.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BggController : ControllerBase
{
    private readonly IMediator _mediator;

    public BggController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<BggSearchResult>>> Search(
        [FromQuery] string query,
        CancellationToken cancellationToken)
    {
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
        var details = await _mediator.Send(new GetBggGameDetailsQuery(bggId), cancellationToken);
        if (details is null)
		{
			return NotFound();
		}

		var command = details.ToAddGameCommand();
        return Ok(command);
    }
}
