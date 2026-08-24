using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HostelSystem.Application.Commands.Rooms;
using HostelSystem.Application.Queries.Rooms;

namespace HostelSystem.Api.Controllers;

// ============================================================
// TEMPLATE for controllers. Every controller follows this pattern:
//   1. Inject IMediator
//   2. Thin actions — map HTTP to command/query, dispatch, map result
//   3. NEVER put business logic here (that's for the domain/handlers)
// ============================================================

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class RoomsController : ControllerBase
{
    private readonly IMediator _mediator;

    public RoomsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get all available rooms in a hostel.
    /// </summary>
    [HttpGet("available/{hostelId:int}")]
    [AllowAnonymous] // Public endpoint — students browse rooms without login
    public async Task<IActionResult> GetAvailable(int hostelId, CancellationToken ct)
    {
        var query = new GetAvailableRoomsQuery(hostelId);
        var rooms = await _mediator.Send(query, ct);
        return Ok(rooms);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] int? hostelId, [FromQuery] bool? isAvailable, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _mediator.Send(new GetRoomsQuery(hostelId, isAvailable, search, page, pageSize), ct);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        Response.Headers["X-Total-Pages"] = result.TotalPages.ToString();
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRoomByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateRoomCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id, version = "1.0" }, result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRoomCommand body, CancellationToken ct)
    {
        if (id != body.Id) return BadRequest(new { error = "ID mismatch." });
        var result = await _mediator.Send(body, ct);
        if (!result.IsSuccess && result.Error?.StartsWith("Conflict") == true) return Conflict(new { error = result.Error });
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteRoomCommand(id), ct);
        return result.IsSuccess ? NoContent() : BadRequest(new { error = result.Error });
    }
}
