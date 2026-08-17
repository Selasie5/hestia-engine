using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}
