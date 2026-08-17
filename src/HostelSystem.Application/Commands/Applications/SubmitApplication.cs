using MediatR;
using HostelSystem.Application.Common;
using HostelSystem.Application.Interfaces;

namespace HostelSystem.Application.Commands.Applications;

// ============================================================
// TEMPLATE: Follow this pattern for every command/query you write.
//
// 1. Record = the command/query DTO (immutable, value equality)
// 2. Handler = the class that does the work
// 3. Validator = FluentValidation rules (optional but recommended)
//
// Pattern: Command : IRequest<Result<TResponse>>
//          Query   : IRequest<TResponse>
// ============================================================

public record SubmitApplicationCommand(
    int StudentId,
    int RoomId,
    string? AdditionalNotes = null
) : IRequest<Result<int>>;

public class SubmitApplicationHandler : IRequestHandler<SubmitApplicationCommand, Result<int>>
{
    private readonly IApplicationRepository _applicationRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitApplicationHandler(
        IApplicationRepository applicationRepository,
        IRoomRepository roomRepository,
        IStudentRepository studentRepository,
        IUnitOfWork unitOfWork)
    {
        _applicationRepository = applicationRepository;
        _roomRepository = roomRepository;
        _studentRepository = studentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<int>> Handle(SubmitApplicationCommand cmd, CancellationToken ct)
    {
        var student = await _studentRepository.GetByIdAsync(cmd.StudentId, ct);
        if (student is null)
            return Result<int>.Fail("Student not found.");

        if (student.HasPendingApplication)
            return Result<int>.Fail("You already have a pending application.");

        if (student.IsCurrentlyAllocated)
            return Result<int>.Fail("You are already allocated to a room.");

        var room = await _roomRepository.GetByIdAsync(cmd.RoomId, ct);
        if (room is null)
            return Result<int>.Fail("Room not found.");

        if (!room.IsAvailable || room.AvailableSpots == 0)
            return Result<int>.Fail($"Room {room.RoomNumber} is not available.");

        var application = new Domain.Entities.RoomApplication(
            cmd.StudentId, cmd.RoomId, cmd.AdditionalNotes);

        await _applicationRepository.AddAsync(application, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<int>.Ok(application.Id);
    }
}
