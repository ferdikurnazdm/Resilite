using System;
using FluentAssertions;

namespace Resilite.UnitTest;

public sealed class CircuitBreakerOptionsValidatorTests
{
    [Fact]
    public void Constructor_WhenOptionsIsNull_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () =>
            new CircuitBreakerPolicy(null!);

        // Assert
        act.Should()
            .Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Constructor_WhenFailureThresholdIsNotPositive_ShouldThrow(
        int failureThreshold)
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = failureThreshold
        };

        // Act
        Action act = () =>
            new CircuitBreakerPolicy(options);

        // Assert
        var assertion = act.Should()
            .Throw<ArgumentOutOfRangeException>();

        assertion.Which.ParamName
            .Should()
            .Be(nameof(CircuitBreakerOptions.FailureThreshold));
    }

    [Fact]
    public void Constructor_WhenBreakDurationIsZero_ShouldThrow()
    {
        var options = new CircuitBreakerOptions
        {
            BreakDuration = TimeSpan.Zero
        };

        Action act = () =>
            new CircuitBreakerPolicy(options);

        var assertion = act.Should()
            .Throw<ArgumentOutOfRangeException>();

        assertion.Which.ParamName
            .Should()
            .Be(nameof(CircuitBreakerOptions.BreakDuration));
    }

    [Fact]
    public void Constructor_WhenBreakDurationIsNegative_ShouldThrow()
    {
        var options = new CircuitBreakerOptions
        {
            BreakDuration = TimeSpan.FromSeconds(-1)
        };

        Action act = () =>
            new CircuitBreakerPolicy(options);

        act.Should()
            .Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WhenShouldHandleIsNull_ShouldThrow()
    {
        var options = new CircuitBreakerOptions
        {
            ShouldHandle = null!
        };

        Action act = () =>
            new CircuitBreakerPolicy(options);

        var assertion = act.Should()
            .Throw<ArgumentNullException>();

        assertion.Which.ParamName
            .Should()
            .Be(nameof(CircuitBreakerOptions.ShouldHandle));
    }

    [Fact]
    public void Constructor_WhenOptionsAreValid_ShouldNotThrow()
    {
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 3,
            BreakDuration = TimeSpan.FromSeconds(30),
            ShouldHandle = _ => true
        };

        Action act = () =>
            new CircuitBreakerPolicy(options);

        act.Should().NotThrow();
    }
}
