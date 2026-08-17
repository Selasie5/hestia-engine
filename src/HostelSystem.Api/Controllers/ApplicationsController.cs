using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HostelSystem.Application.Commands.Applications;

namespace HostelSystem.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize]
public class ApplicationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ApplicationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Submit a new room application.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitApplicationCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(Submit), new { id = result.Value }, result)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// Approve a pending application (Admin only).
    /// </summary>
    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Approve(int id, [FromBody] ApproveApplicationCommand command, CancellationToken ct)
    {
        if (command.ApplicationId != id)
            return BadRequest(new { error = "Application ID mismatch." });

        var result = await _mediator.Send(command, ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }
}
