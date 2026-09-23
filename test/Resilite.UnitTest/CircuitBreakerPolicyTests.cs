using System;
using FluentAssertions;

namespace Resilite.UnitTest;

public class CircuitBreakerPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_WhenFailuresExceedThreshold_ShouldBreakCircuit()
    {
        // Arrange
        var circuitBreakerOptions = new CircuitBreakerOptions
        {
            FailureThreshold = 2,
            BreakDuration = TimeSpan.FromSeconds(5)
        };

        var policy = new CircuitBreakerPolicy(circuitBreakerOptions);

        // Act & Assert 1. Hata
        Func<Task> act1 = async () => await policy.ExecuteAsync<string>(async ct => throw new IOException("Hata 1"), CancellationToken.None);
        await act1.Should().ThrowAsync<IOException>();

        // Act & Assert 2. Hata (Eşik sınıra ulaşıldı, devre açılmalı)
        Func<Task> act2 = async () => await policy.ExecuteAsync<string>(async ct => throw new IOException("Hata 2"), CancellationToken.None);
        await act2.Should().ThrowAsync<IOException>();

        // Act & Assert 3. Deneme: Devre açık olduğu için fonksiyona hiç girmeden CircuitBrokenException fırlatmalı
        Func<Task> act3 = async () => await policy.ExecuteAsync(async ct => Task.FromResult("Çalışmamalı"), CancellationToken.None);

        await act3.Should().ThrowAsync<BrokenCircuitException>()
                 .WithMessage("Circuit is open. The request was blocked.");
    }





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
    public async Task ExecuteAsync_WhenFailureThresholdIsReached_ShouldOpenCircuit()
    {
        // Arrange
        var sut = CreatePolicy(
            failureThreshold: 2,
            breakDuration: TimeSpan.FromMinutes(1));

        // Act
        await Assert.ThrowsAsync<IOException>(() =>
            ExecuteFailureAsync(sut));

        await Assert.ThrowsAsync<IOException>(() =>
            ExecuteFailureAsync(sut));

        Func<Task> act = () =>
            sut.ExecuteAsync<int>(
                _ => Task.FromResult(42),
                CancellationToken.None);

        // Assert
        await act.Should()
            .ThrowAsync<BrokenCircuitException>();
    }

    [Fact]
    public async Task ExecuteAsync_BeforeFailureThreshold_ShouldKeepCircuitClosed()
    {
        // Arrange
        var sut = CreatePolicy(
            failureThreshold: 3,
            breakDuration: TimeSpan.FromMinutes(1));

        // Act
        await Assert.ThrowsAsync<IOException>(() =>
            ExecuteFailureAsync(sut));

        await Assert.ThrowsAsync<IOException>(() =>
            ExecuteFailureAsync(sut));

        var result = await sut.ExecuteAsync(
            _ => Task.FromResult(42),
            CancellationToken.None);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessfulRequestOccurs_ShouldResetFailureCount()
    {
        // Arrange
        var sut = CreatePolicy(
            failureThreshold: 2,
            breakDuration: TimeSpan.FromMinutes(1));

        await Assert.ThrowsAsync<IOException>(() =>
            ExecuteFailureAsync(sut));

        // Act
        await sut.ExecuteAsync(
            _ => Task.FromResult(42),
            CancellationToken.None);

        await Assert.ThrowsAsync<IOException>(() =>
            ExecuteFailureAsync(sut));

        var result = await sut.ExecuteAsync(
            _ => Task.FromResult(100),
            CancellationToken.None);

        // Assert
        result.Should().Be(100);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCircuitIsOpen_ShouldNotExecuteAction()
    {
        // Arrange
        var sut = CreatePolicy(
            failureThreshold: 1,
            breakDuration: TimeSpan.FromMinutes(1));

        await Assert.ThrowsAsync<IOException>(() =>
            ExecuteFailureAsync(sut));

        var executionCount = 0;

        // Act
        Func<Task> act = () =>
            sut.ExecuteAsync<int>(
                _ =>
                {
                    Interlocked.Increment(ref executionCount);

                    return Task.FromResult(42);
                },
                CancellationToken.None);

        // Assert
        await act.Should()
            .ThrowAsync<BrokenCircuitException>();

        executionCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBreakDurationElapsed_ShouldAllowHalfOpenProbe()
    {
        // Arrange
        var sut = CreatePolicy(
            failureThreshold: 1,
            breakDuration: TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<IOException>(() =>
            ExecuteFailureAsync(sut));

        await Task.Delay(100);

        // Act
        var result = await sut.ExecuteAsync(
            _ => Task.FromResult(42),
            CancellationToken.None);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHalfOpenProbeSucceeds_ShouldCloseCircuit()
    {
        // Arrange
        var sut = CreatePolicy(
            failureThreshold: 1,
            breakDuration: TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<IOException>(() =>
            ExecuteFailureAsync(sut));

        await Task.Delay(100);

        await sut.ExecuteAsync(
            _ => Task.FromResult(42),
            CancellationToken.None);

        // Act
        var result = await sut.ExecuteAsync(
            _ => Task.FromResult(100),
            CancellationToken.None);

        // Assert
        result.Should().Be(100);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHalfOpenProbeFails_ShouldReopenCircuit()
    {
        // Arrange
        var sut = CreatePolicy(
            failureThreshold: 1,
            breakDuration: TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<IOException>(() =>
            ExecuteFailureAsync(sut));

        await Task.Delay(100);

        await Assert.ThrowsAsync<IOException>(() =>
            ExecuteFailureAsync(sut));

        // Act
        Func<Task> act = () =>
            sut.ExecuteAsync<int>(
                _ => Task.FromResult(42),
                CancellationToken.None);

        // Assert
        await act.Should()
            .ThrowAsync<BrokenCircuitException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenHalfOpenProbeIsRunning_ShouldRejectConcurrentRequests()
    {
        // Arrange
        var sut = CreatePolicy(
            failureThreshold: 1,
            breakDuration: TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<IOException>(() =>
            ExecuteFailureAsync(sut));

        await Task.Delay(100);

        var probeStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var releaseProbe = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var probe = sut.ExecuteAsync(
            async _ =>
            {
                probeStarted.SetResult(true);

                await releaseProbe.Task;

                return 42;
            },
            CancellationToken.None);

        await probeStarted.Task;

        // Act
        Func<Task> concurrentRequest = () =>
            sut.ExecuteAsync<int>(
                _ => Task.FromResult(100),
                CancellationToken.None);

        // Assert
        await concurrentRequest.Should()
            .ThrowAsync<BrokenCircuitException>();

        releaseProbe.SetResult(true);

        var result = await probe;

        result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_WhenManyRequestsArriveDuringHalfOpen_ShouldAllowOnlyOneProbe()
    {
        // Arrange
        var sut = CreatePolicy(
            failureThreshold: 1,
            breakDuration: TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<IOException>(() =>
            ExecuteFailureAsync(sut));

        await Task.Delay(100);

        var executionCount = 0;

        var releaseProbe = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<int> Action(CancellationToken _)
        {
            Interlocked.Increment(ref executionCount);

            await releaseProbe.Task;

            return 42;
        }

        // Act
        var tasks = Enumerable.Range(0, 50)
            .Select(async _ =>
            {
                try
                {
                    await sut.ExecuteAsync(
                        Action,
                        CancellationToken.None);

                    return true;
                }
                catch (BrokenCircuitException)
                {
                    return false;
                }
            })
            .ToArray();

        await WaitUntilAsync(
            () => Volatile.Read(ref executionCount) == 1);

        executionCount.Should().Be(1);

        releaseProbe.SetResult(true);

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Count(x => x).Should().Be(1);

        executionCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExceptionShouldNotBeHandled_ShouldNotIncrementFailureCount()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            BreakDuration = TimeSpan.FromMinutes(1),
            ShouldHandle = exception =>
                exception is IOException
        };

        var sut = new CircuitBreakerPolicy(options);

        // Act
        Func<Task> unhandledFailure = () =>
            sut.ExecuteAsync<int>(
                _ => throw new InvalidOperationException(),
                CancellationToken.None);

        // Assert
        await unhandledFailure.Should()
            .ThrowAsync<InvalidOperationException>();

        var result = await sut.ExecuteAsync(
            _ => Task.FromResult(42),
            CancellationToken.None);

        result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandledExceptionOccurs_ShouldRethrowOriginalException()
    {
        // Arrange
        var sut = CreatePolicy(
            failureThreshold: 5);

        var expectedException =
            new IOException("Network failure.");

        // Act
        Func<Task> act = () =>
            sut.ExecuteAsync<int>(
                _ => throw expectedException,
                CancellationToken.None);

        // Assert
        var assertion = await act.Should()
            .ThrowAsync<IOException>();

        assertion.Which.Should().BeSameAs(expectedException);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsNotHandled_ShouldPropagateCancellation()
    {
        // Arrange
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            BreakDuration = TimeSpan.FromMinutes(1),
            ShouldHandle = exception =>
                exception is IOException
        };

        var sut = new CircuitBreakerPolicy(options);

        using var cts = new CancellationTokenSource();

        cts.Cancel();

        // Act
        Func<Task> act = () =>
            sut.ExecuteAsync<int>(
                token =>
                {
                    token.ThrowIfCancellationRequested();

                    return Task.FromResult(42);
                },
                cts.Token);

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        var result = await sut.ExecuteAsync(
            _ => Task.FromResult(42),
            CancellationToken.None);

        result.Should().Be(42);
    }

    private static CircuitBreakerPolicy CreatePolicy(
        int failureThreshold = 3,
        TimeSpan? breakDuration = null)
    {
        return new CircuitBreakerPolicy(
            new CircuitBreakerOptions
            {
                FailureThreshold = failureThreshold,
                BreakDuration =
                    breakDuration ?? TimeSpan.FromSeconds(30),

                ShouldHandle = exception =>
                    exception is IOException
            });
    }

    private static Task<int> ExecuteFailureAsync(
        CircuitBreakerPolicy sut)
    {
        return sut.ExecuteAsync<int>(
            _ => throw new IOException("Simulated failure."),
            CancellationToken.None);
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        TimeSpan? timeout = null)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(5);

        var started = DateTime.UtcNow;

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



    [Fact]
    public async Task ExecuteAsync_WhenConcurrentFailuresReachThreshold_ShouldOpenCircuit()
    {
        // Arrange
        const int threshold = 10;

        var sut = CreatePolicy(
            failureThreshold: threshold,
            breakDuration: TimeSpan.FromMinutes(1));

        var gate = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var started = 0;

        async Task<int> FailingAction(CancellationToken _)
        {
            Interlocked.Increment(ref started);

            await gate.Task;

            throw new IOException("Concurrent failure.");
        }

        var tasks = Enumerable.Range(0, threshold)
            .Select(async _ =>
            {
                try
                {
                    await sut.ExecuteAsync(
                        FailingAction,
                        CancellationToken.None);
                }
                catch (IOException)
                {
                    // Expected.
                }
            })
            .ToArray();

        await WaitUntilAsync(
            () => Volatile.Read(ref started) == threshold);

        // Act
        gate.SetResult(true);

        await Task.WhenAll(tasks);

        Func<Task> nextRequest = () =>
            sut.ExecuteAsync<int>(
                _ => Task.FromResult(42),
                CancellationToken.None);

        // Assert
        await nextRequest.Should()
            .ThrowAsync<BrokenCircuitException>();
    }
}
