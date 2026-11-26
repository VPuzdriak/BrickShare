using System;

using AwesomeAssertions;

using Xunit;

namespace BrickShare.SharedKernel.Tests;

public class ResultTests
{
    [Fact]
    public void Success_result_is_successful()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_result_is_not_successful_and_keeps_the_error()
    {
        var error = new DomainError("set.not_available", "The set is not available.");

        var result = Result.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Success_result_carries_the_value()
    {
        var result = Result.Success(42);

        result.Value.Should().Be(42);
    }

    [Fact]
    public void Value_of_a_failed_result_throws()
    {
        var error = new DomainError("set.not_available", "The set is not available.");
        var result = Result.Failure<int>(error);

        Func<int> read = () => result.Value;

        read.Should().Throw<InvalidOperationException>();
    }
}