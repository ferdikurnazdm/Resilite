using System;
using FluentAssertions;

namespace Resilite.UnitTest;

public sealed class RetryOptionsValidatorTests
{
    [Fact]
    public void Validate_WhenOptionsIsNull_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () =>
            RetryOptionsValidator.Validate(null!);

        // Assert
        act.Should()
            .Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Validate_WhenMaxRetryAttemptsIsNotPositive_ShouldThrow(
        int maxRetryAttempts)
    {
        // Arrange
        var options = new RetryOptions
        {
            MaxRetryAttempts = maxRetryAttempts
        };

        // Act
        Action act = () =>
            RetryOptionsValidator.Validate(options);

        // Assert
        var assertion = act.Should()
            .Throw<ArgumentOutOfRangeException>();

        assertion.Which.ParamName.Should()
            .Be(nameof(options.MaxRetryAttempts));

        assertion.Which.ActualValue.Should()
            .Be(maxRetryAttempts);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_WhenDelayIsNotPositive_ShouldThrow(
        int milliseconds)
    {
        // Arrange
        var delay =
            TimeSpan.FromMilliseconds(milliseconds);

        var options = new RetryOptions
        {
            Delay = delay
        };

        // Act
        Action act = () =>
            RetryOptionsValidator.Validate(options);

        // Assert
        var assertion = act.Should()
            .Throw<ArgumentOutOfRangeException>();

        assertion.Which.ParamName.Should()
            .Be(nameof(options.Delay));

        assertion.Which.ActualValue.Should()
            .Be(delay);
    }

    [Fact]
    public void Validate_WhenMaxDelayIsLessThanDelay_ShouldThrow()
    {
        // Arrange
        var options = new RetryOptions
        {
            Delay = TimeSpan.FromSeconds(5),
            MaxDelay = TimeSpan.FromSeconds(4)
        };

        // Act
        Action act = () =>
            RetryOptionsValidator.Validate(options);

        // Assert
        var assertion = act.Should()
            .Throw<ArgumentOutOfRangeException>();

        assertion.Which.ParamName.Should()
            .Be(nameof(options.MaxDelay));

        assertion.Which.ActualValue.Should()
            .Be(options.MaxDelay);
    }

    [Fact]
    public void Validate_WhenMaxDelayEqualsDelay_ShouldNotThrow()
    {
        // Arrange
        var options = new RetryOptions
        {
            Delay = TimeSpan.FromSeconds(5),
            MaxDelay = TimeSpan.FromSeconds(5)
        };

        // Act
        Action act = () =>
            RetryOptionsValidator.Validate(options);

        // Assert
        act.Should()
            .NotThrow();
    }

    [Fact]
    public void Validate_WhenShouldHandleIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new RetryOptions
        {
            ShouldHandle = null!
        };

        // Act
        Action act = () =>
            RetryOptionsValidator.Validate(options);

        // Assert
        act.Should()
            .Throw<ArgumentNullException>()
            .WithParameterName("options.ShouldHandle");
    }

    [Fact]
    public void Validate_WhenOptionsAreValid_ShouldNotThrow()
    {
        // Arrange
        var options = new RetryOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(100),
            MinDelay = TimeSpan.FromMilliseconds(20),
            MaxDelay = TimeSpan.FromSeconds(5),
            ShouldHandle = _ => true
        };

        // Act
        Action act = () =>
            RetryOptionsValidator.Validate(options);

        // Assert
        act.Should()
            .NotThrow();
    }
}
