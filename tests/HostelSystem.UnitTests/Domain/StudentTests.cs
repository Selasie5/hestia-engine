using FluentAssertions;
using HostelSystem.Domain.Entities;
using HostelSystem.Domain.Enums;
using HostelSystem.Domain.Exceptions;

namespace HostelSystem.UnitTests.Domain;

public class StudentTests
{
    // ── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidArgs_SetsProperties()
    {
        var student = new Student("user-1", "STU001", "Jane", "Doe", Gender.Female);

        student.UserId.Should().Be("user-1");
        student.StudentNumber.Should().Be("STU001");
        student.FirstName.Should().Be("Jane");
        student.LastName.Should().Be("Doe");
        student.Gender.Should().Be(Gender.Female);
        student.PhoneNumber.Should().BeNull();
        student.CurrentAllocationId.Should().BeNull();
    }

    [Fact]
    public void FullName_ReturnsFirstAndLastName()
    {
        var student = new Student("u", "S001", "Jane", "Doe", Gender.Female);
        student.FullName.Should().Be("Jane Doe");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyUserId_Throws(string userId)
    {
        Action act = () => new Student(userId, "STU001", "Jane", "Doe", Gender.Female);
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*UserId cannot be empty*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyStudentNumber_Throws(string number)
    {
        Action act = () => new Student("user-1", number, "Jane", "Doe", Gender.Female);
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Student number cannot be empty*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyFirstName_Throws(string firstName)
    {
        Action act = () => new Student("user-1", "STU001", firstName, "Doe", Gender.Female);
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*First name cannot be empty*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyLastName_Throws(string lastName)
    {
        Action act = () => new Student("user-1", "STU001", "Jane", lastName, Gender.Female);
        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*Last name cannot be empty*");
    }

    // ── IsCurrentlyAllocated ──────────────────────────────────────────────────

    [Fact]
    public void IsCurrentlyAllocated_WhenNoAllocation_ReturnsFalse()
    {
        var student = new Student("user-1", "STU001", "Jane", "Doe", Gender.Female);
        student.IsCurrentlyAllocated.Should().BeFalse();
    }

    // HasPendingApplication is driven by the private _applications list which
    // is only populated via EF navigation, so we verify the initial state here.
    [Fact]
    public void HasPendingApplication_Initially_ReturnsFalse()
    {
        var student = new Student("user-1", "STU001", "Jane", "Doe", Gender.Female);
        student.HasPendingApplication.Should().BeFalse();
    }

    // ── UpdateContactInfo ─────────────────────────────────────────────────────

    [Fact]
    public void UpdateContactInfo_SetsPhoneNumber()
    {
        var student = new Student("user-1", "STU001", "Jane", "Doe", Gender.Female);
        student.UpdateContactInfo("+233501234567");

        student.PhoneNumber.Should().Be("+233501234567");
    }

    [Fact]
    public void UpdateContactInfo_NullPhoneNumber_Clears()
    {
        var student = new Student("user-1", "STU001", "Jane", "Doe", Gender.Female);
        student.UpdateContactInfo("+233501234567");
        student.UpdateContactInfo(null);

        student.PhoneNumber.Should().BeNull();
    }
}
