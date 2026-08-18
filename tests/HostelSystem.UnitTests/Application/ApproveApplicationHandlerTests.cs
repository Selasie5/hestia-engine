using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using HostelSystem.Application.Commands.Applications;
using HostelSystem.Application.DTOs;
using HostelSystem.Application.Interfaces;
using HostelSystem.Domain.Entities;
using HostelSystem.Domain.Enums;

namespace HostelSystem.UnitTests.Application;

public class ApproveApplicationHandlerTests
{
    private readonly Mock<IApplicationRepository> _applications = new();
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IStudentRepository> _students = new();
    private readonly Mock<IAllocationRepository> _allocations = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly ApproveApplicationHandler _sut;

    public ApproveApplicationHandlerTests()
    {
        _sut = new ApproveApplicationHandler(
            _applications.Object,
            _rooms.Object,
            _students.Object,
            _allocations.Object,
            _uow.Object,
            _cache.Object);
    }

    [Fact]
    public async Task Handle_ApplicationNotFound_ReturnsFailure()
    {
        _applications.Setup(r => r.GetByIdAsync(It.IsAny<int>(), default))
                     .ReturnsAsync((RoomApplication?)null);

        var result = await _sut.Handle(new ApproveApplicationCommand(1, "admin"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Application not found");
    }

    [Fact]
    public async Task Handle_StudentNotFound_ReturnsFailure()
    {
        var app = new RoomApplication(studentId: 1, roomId: 1);
        _applications.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(app);
        _students.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync((Student?)null);

        var result = await _sut.Handle(new ApproveApplicationCommand(1, "admin"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Student not found");
    }

    [Fact]
    public async Task Handle_RoomNotFound_ReturnsFailure()
    {
        var app = new RoomApplication(studentId: 1, roomId: 99);
        var student = MakeStudent();
        _applications.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(app);
        _students.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(student);
        _rooms.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Room?)null);

        var result = await _sut.Handle(new ApproveApplicationCommand(1, "admin"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Room not found");
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsAllocationDtoAndInvalidatesCache()
    {
        var (app, student, room) = SetupHappyPath();

        SetupRepos(app, student, room);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
        _uow.Setup(u => u.DispatchDomainEventsAsync(default)).Returns(Task.CompletedTask);

        var result = await _sut.Handle(new ApproveApplicationCommand(1, "admin@hostel.com"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<AllocationDto>();
        _cache.Verify(c => c.RemoveAsync($"rooms:available:{room.HostelId}", default), Times.Once);
        _allocations.Verify(r => r.AddAsync(It.IsAny<RoomAllocation>(), default), Times.Once);
    }

    [Fact]
    public async Task Handle_ConcurrencyConflict_Throws()
    {
        var (app, student, room) = SetupHappyPath();
        SetupRepos(app, student, room);

        _uow.Setup(u => u.SaveChangesAsync(default))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        Func<Task> act = () => _sut.Handle(new ApproveApplicationCommand(1, "admin"), default);
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Student MakeStudent() =>
        new("user-1", "STU001", "John", "Doe", Gender.Male);

    private static (RoomApplication app, Student student, Room room) SetupHappyPath()
    {
        var student = MakeStudent();
        var room = new Room("101A", 4, 500m, hostelId: 1);
        var app = new RoomApplication(studentId: student.Id, roomId: room.Id);
        return (app, student, room);
    }

    private void SetupRepos(RoomApplication app, Student student, Room room)
    {
        _applications.Setup(r => r.GetByIdAsync(It.IsAny<int>(), default)).ReturnsAsync(app);
        _students.Setup(r => r.GetByIdAsync(It.IsAny<int>(), default)).ReturnsAsync(student);
        _rooms.Setup(r => r.GetByIdAsync(It.IsAny<int>(), default)).ReturnsAsync(room);
    }
}
