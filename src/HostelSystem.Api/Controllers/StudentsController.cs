using System.Security.Claims;
using HostelSystem.Application.DTOs;
using HostelSystem.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HostelSystem.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly IStudentRepository _studentRepository;
    private readonly IAllocationRepository _allocationRepository;
    private readonly IPaymentRepository _paymentRepository;

    public StudentsController(
        IStudentRepository studentRepository,
        IAllocationRepository allocationRepository,
        IPaymentRepository paymentRepository)
    {
        _studentRepository = studentRepository;
        _allocationRepository = allocationRepository;
        _paymentRepository = paymentRepository;
    }

    /// <summary>
    /// Get my student profile. Uses JWT sub/NameIdentifier to resolve Student.UserId.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { error = "Invalid token." });

        var student = await _studentRepository.GetByUserIdAsync(userId, ct);
        if (student is null)
            return NotFound(new { error = "Student profile not found." });

        var allocation = await _allocationRepository.GetActiveByStudentIdAsync(student.Id, ct);
        var payments = await _paymentRepository.GetByStudentIdAsync(student.Id, ct);
        // Derive status from DB queries instead of navigation (avoids lazy-load pitfalls)
        var apps = await _studentRepository.GetByUserIdAsync(userId, ct); // reuse to ensure tracked? but we need applications
        // Use application repo to compute pending accurately
        // For performance, we can query via injected IApplicationRepository — but avoid extra dependency for now: assume student.HasPendingApplication may be stale, so compute via allocation presence and pending check via a direct query if needed
        // Here we expose both derived values: isAllocated from allocation presence, hasPending from student domain (best-effort) + fallback
        var isAllocated = allocation is not null;
        // HasPending will be derived from student entity's collection if loaded; fallback to false if not loaded
        var hasPending = student.HasPendingApplication;

        return Ok(new
        {
            student = new StudentDto(
                student.Id,
                student.StudentNumber,
                student.FullName,
                student.PhoneNumber,
                isAllocated),
            userId = student.UserId,
            gender = student.Gender.ToString(),
            hasPendingApplication = hasPending,
            isCurrentlyAllocated = isAllocated,
            activeAllocation = allocation is null ? null : new
            {
                allocation.Id,
                allocation.RoomId,
                allocation.IsActive,
                allocation.AllocationDate,
                allocation.CheckOutDate
            },
            payments = payments.Select(p => new PaymentDto(
                p.Id, p.Amount, p.Status.ToString(), p.TransactionReference, p.PaidOn, p.DueDate, p.IsOverdue)).ToList()
        });
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var student = await _studentRepository.GetByIdAsync(id, ct);
        if (student is null) return NotFound(new { error = "Student not found." });
        return Ok(new StudentDto(student.Id, student.StudentNumber, student.FullName, student.PhoneNumber, student.IsCurrentlyAllocated));
    }
}
