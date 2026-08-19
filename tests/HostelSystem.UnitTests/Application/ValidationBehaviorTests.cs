using FluentAssertions;
using FluentValidation;
using HostelSystem.Application.Behaviors;
using HostelSystem.Application.Commands.Applications;
using HostelSystem.Application.Common;
using HostelSystem.Application.Validators;
using MediatR;

namespace HostelSystem.UnitTests.Application;

public class ValidationBehaviorTests
{
    // The real SubmitApplicationValidator — no mocks needed here
    private readonly IValidator<SubmitApplicationCommand> _validator = new SubmitApplicationValidator();

    [Fact]
    public async Task Handle_ValidRequest_CallsNext()
    {
        var behavior = MakeBehavior(_validator);

        var nextCalled = false;
        RequestHandlerDelegate<Result<int>> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result<int>.Ok(1));
        };

        var result = await behavior.Handle(
            new SubmitApplicationCommand(StudentId: 1, RoomId: 1),
            next,
            default);

        nextCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_InvalidStudentId_ReturnsFailure_DoesNotCallNext()
    {
        var behavior = MakeBehavior(_validator);
        var nextCalled = false;
        RequestHandlerDelegate<Result<int>> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result<int>.Ok(1));
        };

        var result = await behavior.Handle(
            new SubmitApplicationCommand(StudentId: 0, RoomId: 1), // invalid
            next,
            default);

        nextCalled.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("StudentId must be greater than 0");
    }

    [Fact]
    public async Task Handle_InvalidRoomId_ReturnsFailure()
    {
        var behavior = MakeBehavior(_validator);
        RequestHandlerDelegate<Result<int>> next = () =>
            Task.FromResult(Result<int>.Ok(1));

        var result = await behavior.Handle(
            new SubmitApplicationCommand(StudentId: 1, RoomId: 0), // invalid
            next,
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("RoomId must be greater than 0");
    }

    [Fact]
    public async Task Handle_NoValidators_CallsNextDirectly()
    {
        var behavior = MakeBehavior(); // no validators registered
        var nextCalled = false;
        RequestHandlerDelegate<Result<int>> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result<int>.Ok(99));
        };

        var result = await behavior.Handle(
            new SubmitApplicationCommand(0, 0), // would normally fail validation
            next,
            default);

        nextCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ValidationBehavior<SubmitApplicationCommand, Result<int>> MakeBehavior(
        params IValidator<SubmitApplicationCommand>[] validators) =>
        new(validators);
}
