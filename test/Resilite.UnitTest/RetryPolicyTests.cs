using System;
using FluentAssertions;
using NSubstitute;

namespace Resilite.UnitTest;

public sealed class RetryPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_WhenOperationSucceeds_ShouldReturnResult()
    {
        // Arrange
        var sut = CreatePolicy();

        // Act
        var result = await sut.ExecuteAsync(
            _ => Task.FromResult(42),
            CancellationToken.None);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationSucceeds_ShouldExecuteOnlyOnce()
    {
        // Arrange
        var sut = CreatePolicy();

        var executionCount = 0;

        // Act
        var result = await sut.ExecuteAsync(
            _ =>
            {
                Interlocked.Increment(ref executionCount);

                return Task.FromResult(42);
            },
            CancellationToken.None);

        // Assert
        result.Should().Be(42);
        executionCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandledExceptionOccurs_ShouldRetry()
    {
        // Arrange
        var sut = CreatePolicy(
            maxRetryAttempts: 3);

        var executionCount = 0;

        // Act
        var result = await sut.ExecuteAsync(
            _ =>
            {
                var attempt =
                    Interlocked.Increment(ref executionCount);

                if (attempt < 3)
                {
                    throw new IOException(
                        "Transient failure.");
                }

                return Task.FromResult(42);
            },
            CancellationToken.None);

        // Assert
        result.Should().Be(42);
        executionCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllAttemptsFail_ShouldExecuteInitialAttemptPlusConfiguredRetries()
    {
        // Arrange
        const int maxRetryAttempts = 3;

        var sut = CreatePolicy(
            maxRetryAttempts: maxRetryAttempts);

        var executionCount = 0;

        // Act
        Func<Task> act = () =>
            sut.ExecuteAsync<int>(
                _ =>
                {
                    Interlocked.Increment(ref executionCount);

                    throw new IOException(
                        "Persistent failure.");
                },
                CancellationToken.None);

        // Assert
        await act.Should()
            .ThrowAsync<IOException>();

        executionCount.Should()
            .Be(1 + maxRetryAttempts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRetryLimitIsExceeded_ShouldRethrowLastException()
    {
        // Arrange
        var sut = CreatePolicy(
            maxRetryAttempts: 2);

        IOException? lastException = null;

        var attempt = 0;

        // Act
        Func<Task> act = () =>
            sut.ExecuteAsync<int>(
                _ =>
                {
                    attempt++;

                    lastException =
                        new IOException(
                            $"Failure #{attempt}");

                    throw lastException;
                },
                CancellationToken.None);

        // Assert
        var assertion = await act.Should()
            .ThrowAsync<IOException>();

        assertion.Which.Should()
            .BeSameAs(lastException);

        attempt.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExceptionShouldNotBeHandled_ShouldNotRetry()
    {
        // Arrange
        var options = CreateOptions();

        options.ShouldHandle =
            exception => exception is IOException;

        var sut = new RetryPolicy(options);

        var executionCount = 0;

        // Act
        Func<Task> act = () =>
            sut.ExecuteAsync<int>(
                _ =>
                {
                    Interlocked.Increment(
                        ref executionCount);

                    throw new InvalidOperationException(
                        "Non-transient failure.");
                },
                CancellationToken.None);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>();

        executionCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandledFailureEventuallySucceeds_ShouldStopRetrying()
    {
        // Arrange
        var sut = CreatePolicy(
            maxRetryAttempts: 5);

        var executionCount = 0;

        // Act
        var result = await sut.ExecuteAsync(
            _ =>
            {
                var attempt =
                    Interlocked.Increment(ref executionCount);

                if (attempt <= 2)
                {
                    throw new IOException();
                }

                return Task.FromResult(42);
            },
            CancellationToken.None);

        // Assert
        result.Should().Be(42);

        executionCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationRequested_ShouldPropagateCancellation()
    {
        // Arrange
        var options = CreateOptions();

        options.ShouldHandle =
            exception => exception is IOException;

        var sut = new RetryPolicy(options);

        using var cts =
            new CancellationTokenSource();

        cts.Cancel();

        var executionCount = 0;

        // Act
        Func<Task> act = () =>
            sut.ExecuteAsync<int>(
                token =>
                {
                    Interlocked.Increment(
                        ref executionCount);

                    token.ThrowIfCancellationRequested();

                    return Task.FromResult(42);
                },
                cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        executionCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledDuringRetryDelay_ShouldStopImmediately()
    {
        // Arrange
        var options = CreateOptions(
            maxRetryAttempts: 10);

        options.Delay =
            TimeSpan.FromSeconds(30);

        options.MinDelay =
            TimeSpan.FromMilliseconds(1);

        options.MaxDelay =
            TimeSpan.FromSeconds(30);

        var sut = new RetryPolicy(options);

        using var cts =
            new CancellationTokenSource();

        var executionCount = 0;

        // Act
        var execution = sut.ExecuteAsync<int>(
            _ =>
            {
                Interlocked.Increment(
                    ref executionCount);

                throw new IOException(
                    "Transient failure.");
            },
            cts.Token);

        await WaitUntilAsync(
            () => Volatile.Read(
                ref executionCount) == 1);

        cts.Cancel();

        // Assert
        Func<Task> act = async () =>
            await execution;

        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        executionCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMultipleInstancesExecuteConcurrently_ShouldKeepAttemptCountsIndependent()
    {
        // Arrange
        var sut = CreatePolicy(
            maxRetryAttempts: 3);

        const int operationCount = 50;

        var executionCounts =
            new int[operationCount];

        // Act
        var tasks = Enumerable
            .Range(0, operationCount)
            .Select(index =>
                sut.ExecuteAsync(
                    _ =>
                    {
                        var attempt =
                            Interlocked.Increment(
                                ref executionCounts[index]);

                        if (attempt < 3)
                        {
                            throw new IOException();
                        }

                        return Task.FromResult(index);
                    },
                    CancellationToken.None))
            .ToArray();

        var results =
            await Task.WhenAll(tasks);

        // Assert
        results.Should()
            .BeEquivalentTo(
                Enumerable.Range(
                    0,
                    operationCount));

        executionCounts.Should()
            .OnlyContain(count => count == 3);
    }

    private static RetryPolicy CreatePolicy(
        int maxRetryAttempts = 3)
    {
        return new RetryPolicy(
            CreateOptions(maxRetryAttempts));
    }

    private static RetryOptions CreateOptions(
        int maxRetryAttempts = 3)
    {
        return new RetryOptions
        {
            MaxRetryAttempts =
                maxRetryAttempts,

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
                    exception is IOException
        };
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        TimeSpan? timeout = null)
    {
        var limit =
            timeout ?? TimeSpan.FromSeconds(5);

        var started =
            DateTime.UtcNow;

        while (!condition())
        {
            if (DateTime.UtcNow - started >= limit)
            {
                throw new TimeoutException(
                    "The expected condition was not reached.");
            }

            await Task.Delay(10);
        }
    }
}
