using System;
using FluentAssertions;

namespace Resilite.UnitTest;

public sealed class ResiliencePipelineTests
{
    [Fact]
    public async Task ExecuteAsync_WhenNoPoliciesConfigured_ShouldExecuteAction()
    {
        // Arrange
        var sut = new ResiliencePipeline();

        // Act
        var result = await sut.ExecuteAsync(
            _ => Task.FromResult(42));

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoPoliciesConfigured_ShouldPropagateException()
    {
        // Arrange
        var sut = new ResiliencePipeline();

        var expected =
            new InvalidOperationException("Failure.");

        // Act
        Func<Task> act = () =>
            sut.ExecuteAsync<int>(
                _ => throw expected);

        // Assert
        var assertion = await act.Should()
            .ThrowAsync<InvalidOperationException>();

        assertion.Which.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task ExecuteAsync_WithRetry_ShouldRetryHandledFailure()
    {
        // Arrange
        var retry = CreateRetryPolicy(
            maxRetryAttempts: 3);

        var sut = new ResiliencePipeline(
            retryPolicy: retry);

        var executionCount = 0;

        // Act
        var result = await sut.ExecuteAsync(
            _ =>
            {
                var attempt =
                    Interlocked.Increment(
                        ref executionCount);

                if (attempt < 3)
                {
                    throw new IOException();
                }

                return Task.FromResult(42);
            });

        // Assert
        result.Should().Be(42);
        executionCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WithTimeout_ShouldThrowResiliteTimeoutException()
    {
        // Arrange
        var timeout = new TimeoutPolicy(
            new TimeoutOptions
            {
                Timeout =
                    TimeSpan.FromMilliseconds(50)
            });

        var sut = new ResiliencePipeline(
            timeoutPolicy: timeout);

        // Act
        Func<Task> act = () =>
            sut.ExecuteAsync<int>(
                async token =>
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(10),
                        token);

                    return 42;
                });

        // Assert
        await act.Should()
            .ThrowAsync<ResilienceTimeoutException>();
    }

    [Fact]
    public async Task ExecuteAsync_WithRetryAndTimeout_ShouldRetryAfterTimeout()
    {
        // Arrange
        var timeout = new TimeoutPolicy(
            new TimeoutOptions
            {
                Timeout =
                    TimeSpan.FromMilliseconds(50)
            });

        var retryOptions = CreateRetryOptions(
            maxRetryAttempts: 3);

        retryOptions.ShouldHandle =
            exception =>
                exception is ResilienceTimeoutException;

        var retry =
            new RetryPolicy(retryOptions);

        var sut = new ResiliencePipeline(
            timeoutPolicy: timeout,
            retryPolicy: retry);

        var executionCount = 0;

        // Act
        var result = await sut.ExecuteAsync(
            async token =>
            {
                var attempt =
                    Interlocked.Increment(
                        ref executionCount);

                if (attempt <= 2)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(10),
                        token);
                }

                return 42;
            });

        // Assert
        result.Should().Be(42);

        executionCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WithRetryAndCircuitBreaker_ShouldCountExhaustedRetrySequenceAsSingleCircuitFailure()
    {
        // Arrange
        var retry = CreateRetryPolicy(
            maxRetryAttempts: 2);

        var circuitBreaker =
            CreateCircuitBreakerPolicy(
                failureThreshold: 2);

        var sut = new ResiliencePipeline(
            retryPolicy: retry,
            circuitBreakerPolicy: circuitBreaker);

        var executionCount = 0;

        async Task ExecuteFailure()
        {
            await sut.ExecuteAsync<int>(
                _ =>
                {
                    Interlocked.Increment(
                        ref executionCount);

                    throw new IOException();
                });
        }

        // Act

        // Pipeline execution #1:
        // initial + 2 retries = 3 action executions
        await Assert.ThrowsAsync<IOException>(
            ExecuteFailure);

        // Circuit failure count should now be 1.
        executionCount.Should().Be(3);

        // Pipeline execution #2:
        // another 3 executions.
        await Assert.ThrowsAsync<IOException>(
            ExecuteFailure);

        executionCount.Should().Be(6);

        // Circuit should now be open.
        Func<Task> thirdExecution =
            ExecuteFailure;

        // Assert
        await thirdExecution.Should()
            .ThrowAsync<BrokenCircuitException>();

        // Action must NOT have executed again.
        executionCount.Should().Be(6);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRetryEventuallySucceeds_ShouldNotCountAsCircuitFailure()
    {
        // Arrange
        var retry = CreateRetryPolicy(
            maxRetryAttempts: 2);

        var circuitBreaker =
            CreateCircuitBreakerPolicy(
                failureThreshold: 1);

        var sut = new ResiliencePipeline(
            retryPolicy: retry,
            circuitBreakerPolicy: circuitBreaker);

        var attempt = 0;

        // Act
        var result = await sut.ExecuteAsync(
            _ =>
            {
                var current =
                    Interlocked.Increment(ref attempt);

                if (current == 1)
                {
                    throw new IOException();
                }

                return Task.FromResult(42);
            });

        // Assert
        result.Should().Be(42);
        attempt.Should().Be(2);

        // Circuit must still allow requests.
        var secondResult = await sut.ExecuteAsync(
            _ => Task.FromResult(100));

        secondResult.Should().Be(100);
    }

    [Fact]
    public async Task ExecuteAsync_WithAllPolicies_ShouldRetryTimeoutAndEventuallySucceedWithoutOpeningCircuit()
    {
        // Arrange
        var timeout = new TimeoutPolicy(
            new TimeoutOptions
            {
                Timeout =
                    TimeSpan.FromMilliseconds(50)
            });

        var retryOptions =
            CreateRetryOptions(
                maxRetryAttempts: 2);

        retryOptions.ShouldHandle =
            exception =>
                exception is ResilienceTimeoutException;

        var retry =
            new RetryPolicy(retryOptions);

        var circuitOptions =
            new CircuitBreakerOptions
            {
                FailureThreshold = 1,

                BreakDuration =
                    TimeSpan.FromMinutes(1),

                ShouldHandle =
                    exception =>
                        exception is
                            ResilienceTimeoutException
                        or IOException
            };

        var circuitBreaker =
            new CircuitBreakerPolicy(
                circuitOptions);

        var sut = new ResiliencePipeline(
            timeout,
            retry,
            circuitBreaker);

        var attempt = 0;

        // Act
        var result = await sut.ExecuteAsync(
            async token =>
            {
                var current =
                    Interlocked.Increment(
                        ref attempt);

                if (current == 1)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(10),
                        token);
                }

                return 42;
            });

        // Assert
        result.Should().Be(42);
        attempt.Should().Be(2);

        // If CircuitBreaker had counted the first
        // timed-out retry as a circuit failure,
        // threshold=1 would have opened it.
        //
        // This proves CircuitBreaker wraps Retry.

        var secondResult = await sut.ExecuteAsync(
            _ => Task.FromResult(100));

        secondResult.Should().Be(100);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExternalCancellationOccurs_ShouldNotRetry()
    {
        // Arrange
        var timeout = new TimeoutPolicy(
            new TimeoutOptions
            {
                Timeout =
                    TimeSpan.FromSeconds(30)
            });

        var retryOptions =
            CreateRetryOptions(
                maxRetryAttempts: 5);

        retryOptions.ShouldHandle =
            exception =>
                exception is IOException
                or ResilienceTimeoutException;

        var retry =
            new RetryPolicy(retryOptions);

        var sut = new ResiliencePipeline(
            timeoutPolicy: timeout,
            retryPolicy: retry);

        using var cts =
            new CancellationTokenSource();

        var executionCount = 0;

        var started =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        var execution = sut.ExecuteAsync<int>(
            async token =>
            {
                Interlocked.Increment(
                    ref executionCount);

                started.TrySetResult(true);

                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    token);

                return 42;
            },
            cts.Token);

        await started.Task;

        // Act
        cts.Cancel();

        Func<Task> act = async () =>
            await execution;

        // Assert
        var assertion = await act.Should()
            .ThrowAsync<OperationCanceledException>();

        assertion.Which.Should()
            .NotBeOfType<ResilienceTimeoutException>();

        executionCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_NonGenericOverload_ShouldExecuteAction()
    {
        // Arrange
        var sut = new ResiliencePipeline();

        var executed = false;

        // Act
        await sut.ExecuteAsync(
            _ =>
            {
                executed = true;

                return Task.CompletedTask;
            });

        // Assert
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_NonGenericOverload_ShouldPropagateException()
    {
        // Arrange
        var sut = new ResiliencePipeline();

        var expected =
            new InvalidOperationException(
                "Failure.");

        // Act
        Func<Task> act = () =>
            sut.ExecuteAsync(
                _ => throw expected);

        // Assert
        var assertion = await act.Should()
            .ThrowAsync<InvalidOperationException>();

        assertion.Which.Should()
            .BeSameAs(expected);
    }

    private static RetryPolicy CreateRetryPolicy(
        int maxRetryAttempts)
    {
        return new RetryPolicy(
            CreateRetryOptions(
                maxRetryAttempts));
    }

    private static RetryOptions CreateRetryOptions(
        int maxRetryAttempts)
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

    private static CircuitBreakerPolicy
        CreateCircuitBreakerPolicy(
            int failureThreshold)
    {
        return new CircuitBreakerPolicy(
            new CircuitBreakerOptions
            {
                FailureThreshold =
                    failureThreshold,

                BreakDuration =
                    TimeSpan.FromMinutes(1),

                ShouldHandle =
                    exception =>
                        exception is IOException
            });
    }
}
