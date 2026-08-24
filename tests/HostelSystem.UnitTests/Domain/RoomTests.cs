using FluentAssertions;
using HostelSystem.Domain.Entities;
using HostelSystem.Domain.Exceptions;

namespace HostelSystem.UnitTests.Domain;

public class RoomTests
{
    // ── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidArgs_SetsPropertiesCorrectly()
    {
        var room = new Room("101A", 4, 500m, hostelId: 1);

        room.RoomNumber.Should().Be("101A");
        room.Capacity.Should().Be(4);
        room.PricePerSemester.Should().Be(500m);
        room.HostelId.Should().Be(1);
        room.CurrentOccupancy.Should().Be(0);
        room.IsAvailable.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyRoomNumber_Throws(string roomNumber)
    {
        Action act = () => new Room(roomNumber, 4, 500m, hostelId: 1);
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Room number cannot be empty*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveCapacity_Throws(int capacity)
    {
        Action act = () => new Room("101A", capacity, 500m, hostelId: 1);
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*capacity must be greater than zero*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Constructor_NonPositivePrice_Throws(decimal price)
    {
        Action act = () => new Room("101A", 4, price, hostelId: 1);
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Price must be greater than zero*");
    }

    // ── AllocateStudent ───────────────────────────────────────────────────────

    [Fact]
    public void AllocateStudent_IncrementsOccupancy()
    {
        var room = new Room("101A", 4, 500m, hostelId: 1);
        var student = MakeStudent();

        room.AllocateStudent(student);

        room.CurrentOccupancy.Should().Be(1);
        room.IsAvailable.Should().BeTrue();   // still space left
    }

    [Fact]
    public void AllocateStudent_WhenRoomBecomesFull_SetsIsAvailableFalse()
    {
        var room = new Room("101A", 1, 500m, hostelId: 1);
        var student = MakeStudent();

        room.AllocateStudent(student);

        room.CurrentOccupancy.Should().Be(1);
        room.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void AllocateStudent_WhenNotAvailable_Throws()
    {
        // Fill the room first
        var room = new Room("101A", 1, 500m, hostelId: 1);
        room.AllocateStudent(MakeStudent());

        Action act = () => room.AllocateStudent(MakeStudent());
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*not available*");
    }

    [Fact]
    public void AllocateStudent_AddsDomainEvent()
    {
        var room = new Room("101A", 4, 500m, hostelId: 1);
        room.AllocateStudent(MakeStudent());

        room.DomainEvents.Should().ContainSingle();
    }

    // ── ReleaseStudent ────────────────────────────────────────────────────────

    [Fact]
    public void ReleaseStudent_DecrementsOccupancyAndSetsAvailable()
    {
        var room = new Room("101A", 1, 500m, hostelId: 1);
        room.AllocateStudent(MakeStudent());

        room.ReleaseStudent();

        room.CurrentOccupancy.Should().Be(0);
        room.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void ReleaseStudent_WhenNoOccupants_Throws()
    {
        var room = new Room("101A", 4, 500m, hostelId: 1);

        Action act = () => room.ReleaseStudent();
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*no occupants to release*");
    }

    // ── AvailableSpots ────────────────────────────────────────────────────────

    [Fact]
    public void AvailableSpots_ReturnsCapacityMinusOccupancy()
    {
        var room = new Room("101A", 4, 500m, hostelId: 1);
        room.AllocateStudent(MakeStudent());

        room.AvailableSpots.Should().Be(3);
    }

    // ── UpdatePrice ───────────────────────────────────────────────────────────

    [Fact]
    public void UpdatePrice_ValidPrice_UpdatesSuccessfully()
    {
        var room = new Room("101A", 4, 500m, hostelId: 1);
        room.UpdatePrice(750m);

        room.PricePerSemester.Should().Be(750m);
    }

    [Fact]
    public void UpdatePrice_NonPositivePrice_Throws()
    {
        var room = new Room("101A", 4, 500m, hostelId: 1);
        Action act = () => room.UpdatePrice(0m);
        act.Should().Throw<BusinessRuleViolationException>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Student MakeStudent() =>
        new("user-abc", "STU001", "John", "Doe", global::HostelSystem.Domain.Enums.Gender.Male);
}
