using System;
using FluentAssertions;

namespace Resilite.UnitTest;

public sealed class TimeoutOptionsValidatorTests
{
    [Fact]
    public void Validate_WhenOptionsIsNull_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () =>
            TimeoutOptionsValidator.Validate(null!);

        // Assert
        act.Should()
            .Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public void Validate_WhenTimeoutIsZero_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var options = new TimeoutOptions
        {
            Timeout = TimeSpan.Zero
        };

        // Act
        Action act = () =>
            TimeoutOptionsValidator.Validate(options);

        // Assert
        var assertion = act.Should()
            .Throw<ArgumentOutOfRangeException>();

        assertion.Which.ParamName.Should()
            .Be(nameof(options.Timeout));

        assertion.Which.ActualValue.Should()
            .Be(TimeSpan.Zero);
    }

    [Fact]
    public void Validate_WhenTimeoutIsNegative_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var timeout =
            TimeSpan.FromMilliseconds(-1);

        var options = new TimeoutOptions
        {
            Timeout = timeout
        };

        // Act
        Action act = () =>
            TimeoutOptionsValidator.Validate(options);

        // Assert
        var assertion = act.Should()
            .Throw<ArgumentOutOfRangeException>();

        assertion.Which.ParamName.Should()
            .Be(nameof(options.Timeout));

        assertion.Which.ActualValue.Should()
            .Be(timeout);
    }

    [Fact]
    public void Validate_WhenTimeoutIsPositive_ShouldNotThrow()
    {
        // Arrange
        var options = new TimeoutOptions
        {
            Timeout = TimeSpan.FromMilliseconds(1)
        };

        // Act
        Action act = () =>
            TimeoutOptionsValidator.Validate(options);

        // Assert
        act.Should()
            .NotThrow();
    }
}
