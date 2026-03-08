using BggIntegration.Application.Queries;
using BggIntegration.Domain.Interfaces;
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
    private readonly IBggTranslator _translator;

    public BggController(IMediator mediator, IBggTranslator translator)
    {
        _mediator = mediator;
        _translator = translator;
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<BggSearchResult>>> Search(
        [FromQuery] string q,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("Query parameter 'q' is required.");

        var results = await _mediator.Send(new SearchBggQuery(q), cancellationToken);
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
        if (details is null) return NotFound();

        var command = _translator.ToAddGameCommand(details);
        return Ok(command);
    }
}
