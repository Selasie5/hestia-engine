using System.Security.Claims;
using HostelSystem.Application.Interfaces;
using HostelSystem.Application.Commands.Applications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HostelSystem.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize]
public class ApplicationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IStudentRepository _studentRepository;
    private readonly IApplicationRepository _applicationRepository;

    public ApplicationsController(IMediator mediator, IStudentRepository studentRepository, IApplicationRepository applicationRepository)
    {
        _mediator = mediator;
        _studentRepository = studentRepository;
        _applicationRepository = applicationRepository;
    }

    /// <summary>GET my applications (Student). Admin can still call but gets own.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMy(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized(new { error = "Invalid token." });
        var student = await _studentRepository.GetByUserIdAsync(userId, ct);
        if (student is null) return NotFound(new { error = "Student profile not found." });
        var apps = await _applicationRepository.GetByStudentIdAsync(student.Id, ct);
        var dtos = apps.Select(a => new
        {
            a.Id, a.StudentId, a.RoomId, Status = a.Status.ToString(), a.ApplicationDate, a.ReviewedOn, a.ReviewedBy, a.RejectionReason, a.AdditionalNotes,
            roomNumber = a.Room?.RoomNumber, hostelName = a.Room?.Hostel?.Name
        });
        return Ok(dtos);
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

    public record RejectRequest(string Reason);

    /// <summary>
    /// Reject a pending application (Admin only).
    /// </summary>
    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body?.Reason))
            return BadRequest(new { error = "Rejection reason is required." });

        var reviewedBy = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email) ?? "admin";
        var cmd = new RejectApplicationCommand(id, reviewedBy, body.Reason);
        var result = await _mediator.Send(cmd, ct);
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { error = result.Error });
    }

    public record CancelRequest(string? Reason);

    /// <summary>
    /// Cancel own application (Student owns) or Admin can cancel any. Triggers refund if payment completed.
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    [Authorize]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelRequest? body, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Invalid token." });

        var isAdmin = User.IsInRole("Admin");
        var cmd = new CancelApplicationCommand(id, userId, isAdmin, body?.Reason);
        var result = await _mediator.Send(cmd, ct);
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { error = result.Error });
    }
}
