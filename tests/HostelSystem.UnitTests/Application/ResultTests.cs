using FluentAssertions;
using HostelSystem.Application.Common;

namespace HostelSystem.UnitTests.Application;

public class ResultTests
{
    // ── Non-generic Result ────────────────────────────────────────────────────

    [Fact]
    public void Result_Ok_IsSuccessTrue_ErrorNull()
    {
        var result = Result.Ok();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Result_Fail_IsSuccessFalse_ErrorSet()
    {
        var result = Result.Fail("something went wrong");

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("something went wrong");
    }

    // ── Generic Result<T> ─────────────────────────────────────────────────────

    [Fact]
    public void ResultT_Ok_IsSuccessTrue_ValueSet()
    {
        var result = Result<int>.Ok(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void ResultT_Fail_IsSuccessFalse_ValueDefault()
    {
        var result = Result<int>.Fail("not found");

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Value.Should().Be(default(int));
        result.Error.Should().Be("not found");
    }

    [Fact]
    public void ResultT_Fail_WithReferenceType_ValueIsNull()
    {
        var result = Result<string>.Fail("boom");

        result.Value.Should().BeNull();
    }
}
