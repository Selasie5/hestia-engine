using System.Security.Claims;
using HostelSystem.Application.Commands.Allocations;
using HostelSystem.Application.DTOs;
using HostelSystem.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HostelSystem.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize]
public class AllocationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAllocationRepository _allocationRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IRoomRepository _roomRepository;

    public AllocationsController(
        IMediator mediator,
        IAllocationRepository allocationRepository,
        IStudentRepository studentRepository,
        IRoomRepository roomRepository)
    {
        _mediator = mediator;
        _allocationRepository = allocationRepository;
        _studentRepository = studentRepository;
        _roomRepository = roomRepository;
    }

    /// <summary>
    /// Get my current (active) allocation. Students: own. Admin: can query via ?studentId.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMy(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Invalid token." });

        var student = await _studentRepository.GetByUserIdAsync(userId, ct);
        if (student is null)
            return NotFound(new { error = "Student profile not found. Complete registration." });

        var allocation = await _allocationRepository.GetActiveByStudentIdAsync(student.Id, ct);
        if (allocation is null)
            return NotFound(new { error = "No active allocation." });

        // Need room + hostel for DTO; allocation from repo may already include, but fetch room for safety
        var room = await _roomRepository.GetByIdAsync(allocation.RoomId, ct);
        return Ok(new AllocationDto(
            allocation.Id,
            allocation.StudentId,
            student.FullName,
            allocation.RoomId,
            room?.RoomNumber ?? "N/A",
            room?.Hostel?.Name ?? "N/A",
            allocation.AllocationDate,
            allocation.IsActive));
    }

    /// <summary>
    /// Checkout an allocation — Student (own) or Admin. Releases room occupancy.
    /// </summary>
    [HttpPost("{id:int}/checkout")]
    public async Task<IActionResult> Checkout(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.Email) ?? "";
        var isAdmin = User.IsInRole("Admin");
        var cmd = new CheckoutAllocationCommand(id, userId, isAdmin);
        var result = await _mediator.Send(cmd, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Admin: list allocations paged.</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool? isActive = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _allocationRepository.GetPagedAsync(page, pageSize, isActive, ct);
        Response.Headers["X-Total-Count"] = total.ToString();
        var dtos = items.Select(a => new AllocationDto(a.Id, a.StudentId, a.Student?.FullName ?? a.StudentId.ToString(), a.RoomId, a.Room?.RoomNumber ?? a.RoomId.ToString(), a.Room?.Hostel?.Name ?? "N/A", a.AllocationDate, a.IsActive));
        return Ok(new { items = dtos, total, page, pageSize });
    }

    /// <summary>
    /// Get allocation by id (Admin or owner).
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var allocation = await _allocationRepository.GetByIdAsync(id, ct);
        if (allocation is null)
            return NotFound(new { error = "Allocation not found." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && allocation.Student.UserId != userId)
            return Forbid();

        return Ok(new AllocationDto(
            allocation.Id,
            allocation.StudentId,
            allocation.Student?.FullName ?? "N/A",
            allocation.RoomId,
            allocation.Room?.RoomNumber ?? "N/A",
            allocation.Room?.Hostel?.Name ?? "N/A",
            allocation.AllocationDate,
            allocation.IsActive));
    }
}
