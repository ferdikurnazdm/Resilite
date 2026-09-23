using System;
using System.Net.Sockets;
using FluentAssertions;

namespace Resilite.UnitTest;

public sealed class ShouldHandleTests
{
    [Theory]
    [MemberData(nameof(DefaultHandledExceptions))]
    public void DefaultPredicate_WhenExceptionIsTransient_ShouldReturnTrue(
        Exception exception)
    {
        // Act
        var result =
            DefaultExceptionPredicates.ShouldHandle(
                exception);

        // Assert
        result.Should().BeTrue();
    }

    public static IEnumerable<object[]> DefaultHandledExceptions()
    {
        yield return
        [
            new TimeoutException()
        ];

        yield return
        [
            new IOException()
        ];

        yield return
        [
            new SocketException()
        ];

        yield return
        [
            new HttpRequestException()
        ];

        yield return
        [
            new ResilienceTimeoutException(
                TimeSpan.FromSeconds(1))
        ];
    }

    [Theory]
    [MemberData(nameof(DefaultUnhandledExceptions))]
    public void DefaultPredicate_WhenExceptionIsNotTransient_ShouldReturnFalse(
        Exception exception)
    {
        var result =
            DefaultExceptionPredicates.ShouldHandle(
                exception);

        result.Should().BeFalse();
    }

    public static IEnumerable<object[]> DefaultUnhandledExceptions()
    {
        yield return
        [
            new ArgumentException()
        ];

        yield return
        [
            new InvalidOperationException()
        ];

        yield return
        [
            new NullReferenceException()
        ];

        yield return
        [
            new OperationCanceledException()
        ];
    }

    [Fact]
    public async Task Retry_WhenDeveloperOverridesShouldHandle_ShouldUseCustomPredicate()
    {
        // Arrange
        var options = new RetryOptions
        {
            MaxRetryAttempts = 2,

            Delay =
                TimeSpan.FromMilliseconds(1),

            MinDelay =
                TimeSpan.FromMilliseconds(1),

            MaxDelay =
                TimeSpan.FromMilliseconds(10),

            UseExponentialBackoff = false,

            Jitter = JitterMode.None,

            ShouldHandle =
                exception =>
                    exception is
                        InvalidOperationException
        };

        var sut = new RetryPolicy(options);

        var executionCount = 0;

        // Act
        Func<Task> act = () =>
            sut.ExecuteAsync<int>(
                _ =>
                {
                    Interlocked.Increment(
                        ref executionCount);

                    throw new InvalidOperationException();
                },
                CancellationToken.None);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>();

        executionCount.Should().Be(3);
    }

    [Fact]
    public async Task CircuitBreaker_WhenDeveloperOverridesShouldHandle_ShouldUseCustomPredicate()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 1,

            BreakDuration =
                TimeSpan.FromMinutes(1),

            ShouldHandle =
                exception =>
                    exception is
                        InvalidOperationException
        };

        var sut =
            new CircuitBreakerPolicy(options);

        // Act
        Func<Task> first = () =>
            sut.ExecuteAsync<int>(
                _ =>
                    throw new InvalidOperationException(),
                CancellationToken.None);

        await first.Should()
            .ThrowAsync<InvalidOperationException>();

        Func<Task> second = () =>
            sut.ExecuteAsync(
                _ => Task.FromResult(42),
                CancellationToken.None);

        // Assert
        await second.Should()
            .ThrowAsync<BrokenCircuitException>();
    }

    [Fact]
    public async Task Retry_WhenCustomPredicateReturnsFalse_ShouldNotRetry()
    {
        var options = new RetryOptions
        {
            MaxRetryAttempts = 10,

            Delay =
                TimeSpan.FromMilliseconds(1),

            MinDelay =
                TimeSpan.FromMilliseconds(1),

            MaxDelay =
                TimeSpan.FromMilliseconds(10),

            ShouldHandle = _ => false
        };

        var sut = new RetryPolicy(options);

        var count = 0;

        Func<Task> act = () =>
            sut.ExecuteAsync<int>(
                _ =>
                {
                    Interlocked.Increment(ref count);

                    throw new IOException();
                },
                CancellationToken.None);

        await act.Should()
            .ThrowAsync<IOException>();

        count.Should().Be(1);
    }

    [Fact]
    public async Task Retry_WhenShouldHandleThrows_ShouldPropagateOriginalException()
    {
        // Arrange
        var options = new RetryOptions
        {
            MaxRetryAttempts = 3,

            ShouldHandle = _ =>
                throw new InvalidOperationException(
                    "Predicate failed.")
        };

        var sut = new RetryPolicy(options);

        var originalException =
            new IOException("I/O error occurred.");

        // Act
        Func<Task> act = () =>
            sut.ExecuteAsync<int>(
                _ => throw originalException,
                CancellationToken.None);

        // Assert
        var assertion = await act.Should()
            .ThrowAsync<IOException>();

        assertion.Which.Should()
            .BeSameAs(originalException);
    }
}
