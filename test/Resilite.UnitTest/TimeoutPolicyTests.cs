using System;
using FluentAssertions;

namespace Resilite.UnitTest;

public sealed class TimeoutPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_WhenOperationCompletesBeforeTimeout_ShouldReturnResult()
    {
        // Arrange
        var sut = CreatePolicy(
            TimeSpan.FromSeconds(1));

        // Act
        var result = await sut.ExecuteAsync(
            _ => Task.FromResult(42),
            CancellationToken.None);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationExceedsTimeout_ShouldThrowResiliteTimeoutException()
    {
        // Arrange
        var timeout =
            TimeSpan.FromMilliseconds(50);

        var sut = CreatePolicy(timeout);

        // Act
        Func<Task> act = () =>
            sut.ExecuteAsync<int>(
                async token =>
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(10),
                        token);

                    return 42;
                },
                CancellationToken.None);

        // Assert
        var assertion = await act.Should()
            .ThrowAsync<ResilienceTimeoutException>();

        assertion.Which.Timeout
            .Should()
            .Be(timeout);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTimeoutOccurs_ShouldPreserveOriginalCancellationException()
    {
        // Arrange
        var sut = CreatePolicy(
            TimeSpan.FromMilliseconds(50));

        // Act
        Func<Task> act = () =>
            sut.ExecuteAsync<int>(
                async token =>
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(10),
                        token);

                    return 42;
                },
                CancellationToken.None);

        // Assert
        var assertion = await act.Should()
            .ThrowAsync<ResilienceTimeoutException>();

        assertion.Which.InnerException
            .Should()
            .BeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenExternalCancellationIsRequested_ShouldNotConvertToTimeoutException()
    {
        // Arrange
        var sut = CreatePolicy(
            TimeSpan.FromSeconds(30));

        using var cts =
            new CancellationTokenSource();

        // Act
        var execution = sut.ExecuteAsync<int>(
            async token =>
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(30),
                    token);

                return 42;
            },
            cts.Token);

        cts.Cancel();

        Func<Task> act = async () =>
            await execution;

        // Assert
        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        await act.Should()
            .NotThrowAsync<ResilienceTimeoutException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionThrowsException_ShouldPropagateOriginalException()
    {
        // Arrange
        var sut = CreatePolicy(
            TimeSpan.FromSeconds(1));

        var expectedException =
            new InvalidOperationException(
                "Application failure.");

        // Act
        Func<Task> act = () =>
            sut.ExecuteAsync<int>(
                _ => throw expectedException,
                CancellationToken.None);

        // Assert
        var assertion = await act.Should()
            .ThrowAsync<InvalidOperationException>();

        assertion.Which.Should()
            .BeSameAs(expectedException);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionThrowsHandledLookingExceptionBeforeTimeout_ShouldNotConvertIt()
    {
        // Arrange
        var sut = CreatePolicy(
            TimeSpan.FromSeconds(1));

        var expectedException =
            new IOException(
                "Network failure.");

        // Act
        Func<Task> act = () =>
            sut.ExecuteAsync<int>(
                _ => throw expectedException,
                CancellationToken.None);

        // Assert
        var assertion = await act.Should()
            .ThrowAsync<IOException>();

        assertion.Which.Should()
            .BeSameAs(expectedException);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassCancellationTokenToAction()
    {
        // Arrange
        var sut = CreatePolicy(
            TimeSpan.FromSeconds(1));

        CancellationToken receivedToken =
            default;

        // Act
        await sut.ExecuteAsync(
            token =>
            {
                receivedToken = token;

                return Task.FromResult(42);
            },
            CancellationToken.None);

        // Assert
        receivedToken.CanBeCanceled
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenExternalTokenIsCancelled_ShouldCancelTokenPassedToAction()
    {
        // Arrange
        var sut = CreatePolicy(
            TimeSpan.FromSeconds(30));

        using var cts =
            new CancellationTokenSource();

        var actionStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var cancellationObserved =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var execution = sut.ExecuteAsync<int>(
            async token =>
            {
                actionStarted.SetResult(true);

                try
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        token);
                }
                catch (OperationCanceledException)
                {
                    cancellationObserved.SetResult(true);

                    throw;
                }

                return 42;
            },
            cts.Token);

        await actionStarted.Task;

        cts.Cancel();

        // Assert
        await cancellationObserved.Task;

        Func<Task> act = async () =>
            await execution;

        await act.Should()
            .ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenManyOperationsRunConcurrently_ShouldKeepTimeoutsIndependent()
    {
        // Arrange
        var sut = CreatePolicy(
            TimeSpan.FromMilliseconds(100));

        const int operationCount = 50;

        // Act
        var tasks = Enumerable
            .Range(0, operationCount)
            .Select(index =>
                sut.ExecuteAsync(
                    async token =>
                    {
                        await Task.Delay(
                            TimeSpan.FromMilliseconds(10),
                            token);

                        return index;
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
    }

    [Fact]
    public async Task ExecuteAsync_WhenManyOperationsTimeoutConcurrently_ShouldThrowTimeoutForEachOperation()
    {
        // Arrange
        var sut = CreatePolicy(
            TimeSpan.FromMilliseconds(50));

        const int operationCount = 25;

        async Task Execute()
        {
            await sut.ExecuteAsync<int>(
                async token =>
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(10),
                        token);

                    return 42;
                },
                CancellationToken.None);
        }

        // Act
        var tasks = Enumerable
            .Range(0, operationCount)
            .Select(async _ =>
            {
                Func<Task> act = Execute;

                await act.Should()
                    .ThrowAsync<ResilienceTimeoutException>();
            });

        // Assert
        await Task.WhenAll(tasks);
    }

    private static TimeoutPolicy CreatePolicy(
        TimeSpan timeout)
    {
        return new TimeoutPolicy(
            new TimeoutOptions
            {
                Timeout = timeout
            });
    }
}
