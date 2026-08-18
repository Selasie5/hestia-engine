using FluentAssertions;
using Moq;
using HostelSystem.Application.Commands.Applications;
using HostelSystem.Application.Interfaces;
using HostelSystem.Domain.Entities;
using HostelSystem.Domain.Enums;

namespace HostelSystem.UnitTests.Application;

public class SubmitApplicationHandlerTests
{
    private readonly Mock<IStudentRepository> _students = new();
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IApplicationRepository> _applications = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly SubmitApplicationHandler _sut;

    public SubmitApplicationHandlerTests()
    {
        _sut = new SubmitApplicationHandler(
            _applications.Object,
            _rooms.Object,
            _students.Object,
            _uow.Object);
    }

    [Fact]
    public async Task Handle_StudentNotFound_ReturnsFailure()
    {
        _students.Setup(r => r.GetByIdAsync(It.IsAny<int>(), default))
                 .ReturnsAsync((Student?)null);

        var result = await _sut.Handle(new SubmitApplicationCommand(1, 1), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Student not found");
    }

    [Fact]
    public async Task Handle_StudentHasPendingApplication_ReturnsFailure()
    {
        // A student with a pending application in their collection
        var student = MakeStudentWithPendingApp();
        _students.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(student);

        var result = await _sut.Handle(new SubmitApplicationCommand(1, 1), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("pending application");
    }

    [Fact]
    public async Task Handle_StudentAlreadyAllocated_ReturnsFailure()
    {
        var student = MakeAllocatedStudent();
        _students.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(student);

        var result = await _sut.Handle(new SubmitApplicationCommand(1, 1), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already allocated");
    }

    [Fact]
    public async Task Handle_RoomNotFound_ReturnsFailure()
    {
        var student = MakeStudent();
        _students.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(student);
        _rooms.Setup(r => r.GetByIdAsync(It.IsAny<int>(), default))
              .ReturnsAsync((Room?)null);

        var result = await _sut.Handle(new SubmitApplicationCommand(1, 99), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Room not found");
    }

    [Fact]
    public async Task Handle_RoomNotAvailable_ReturnsFailure()
    {
        var student = MakeStudent();
        var room = MakeFullRoom();
        _students.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(student);
        _rooms.Setup(r => r.GetByIdAsync(2, default)).ReturnsAsync(room);

        var result = await _sut.Handle(new SubmitApplicationCommand(1, 2), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not available");
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesApplicationAndReturnsId()
    {
        var student = MakeStudent();
        var room = MakeAvailableRoom();
        _students.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(student);
        _rooms.Setup(r => r.GetByIdAsync(2, default)).ReturnsAsync(room);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await _sut.Handle(new SubmitApplicationCommand(1, 2, "Window seat please"), default);

        result.IsSuccess.Should().BeTrue();
        _applications.Verify(r => r.AddAsync(It.IsAny<RoomApplication>(), default), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Student MakeStudent() =>
        new("user-1", "STU001", "John", "Doe", Gender.Male);

    /// Student whose HasPendingApplication returns true — we use a subclass
    /// or a real object with a pending app added via reflection on the field.
    private static Student MakeStudentWithPendingApp()
    {
        var student = new Student("user-1", "STU001", "John", "Doe", Gender.Male);
        // Inject a pending RoomApplication into the private _applications list
        var field = typeof(Student)
            .GetField("_applications", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var list = (List<RoomApplication>)field.GetValue(student)!;
        list.Add(new RoomApplication(student.Id, 1));   // status = Pending
        return student;
    }

    private static Student MakeAllocatedStudent()
    {
        var student = new Student("user-2", "STU002", "Jane", "Doe", Gender.Female);
        // Inject CurrentAllocationId via reflection
        typeof(Student)
            .GetProperty("CurrentAllocationId")!
            .SetValue(student, 99);
        return student;
    }

    private static Room MakeAvailableRoom() => new("101A", 4, 500m, hostelId: 1);

    private static Room MakeFullRoom()
    {
        var room = new Room("101B", 1, 500m, hostelId: 1);
        var student = new Student("user-x", "STU999", "X", "X", Gender.Male);
        room.AllocateStudent(student);   // fills the room (capacity=1)
        return room;
    }
}
