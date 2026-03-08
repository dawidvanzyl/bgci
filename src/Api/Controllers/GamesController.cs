using GameCollection.Application.Commands;
using GameCollection.Application.DTOs;
using GameCollection.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GamesController : ControllerBase
{
    private readonly IMediator _mediator;

    public GamesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CollectedGameDto>>> GetAll(CancellationToken cancellationToken)
    {
        var games = await _mediator.Send(new GetAllGamesQuery(), cancellationToken);
        return Ok(games);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CollectedGameDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var game = await _mediator.Send(new GetGameByIdQuery(id), cancellationToken);
        return game is null ? NotFound() : Ok(game);
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> AddManually(
        [FromBody] AddGameManuallyCommand command,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPost("from-bgg")]
    public async Task<ActionResult<Guid>> AddFromBgg(
        [FromBody] AddGameFromBggCommand command,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateGameRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateGameCommand(
            id,
            request.Name,
            request.Year,
            request.Description,
            request.MinPlayers,
            request.MaxPlayers,
            request.PlayTimeMinutes,
            request.BggRating,
            request.CoverImageUrl,
            request.Categories,
            request.Mechanics
        );

        try
        {
            await _mediator.Send(command, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteGameCommand(id), cancellationToken);
        return NoContent();
    }
}

public record UpdateGameRequest(
    string Name,
    int? Year,
    string? Description,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayTimeMinutes,
    decimal? BggRating,
    string? CoverImageUrl,
    List<string>? Categories,
    List<string>? Mechanics
);
