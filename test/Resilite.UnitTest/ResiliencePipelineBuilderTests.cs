using System;
using FluentAssertions;

namespace Resilite.UnitTest;

public sealed class ResiliencePipelineBuilderTests
{
    [Fact]
    public void AddTimeout_WhenConfigureIsNull_ShouldThrow()
    {
        var sut = new ResiliencePipelineBuilder();

        Action act = () =>
            sut.AddTimeout(null!);

        act.Should()
            .Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddRetry_WhenConfigureIsNull_ShouldThrow()
    {
        var sut = new ResiliencePipelineBuilder();

        Action act = () =>
            sut.AddRetry(null!);

        act.Should()
            .Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddCircuitBreaker_WhenConfigureIsNull_ShouldThrow()
    {
        var sut = new ResiliencePipelineBuilder();

        Action act = () =>
            sut.AddCircuitBreaker(null!);

        act.Should()
            .Throw<ArgumentNullException>();
    }

    [Fact]
    public void Build_WhenNoPoliciesConfigured_ShouldReturnPipeline()
    {
        var sut = new ResiliencePipelineBuilder();

        var result = sut.Build();

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IResiliencePipeline>();
    }

    [Fact]
    public async Task AddTimeout_ShouldConfigureTimeoutPolicy()
    {
        var sut = new ResiliencePipelineBuilder();

        var pipeline = sut
            .AddTimeout(options =>
            {
                options.Timeout =
                    TimeSpan.FromMilliseconds(50);
            })
            .Build();

        Func<Task> act = () =>
            pipeline.ExecuteAsync<int>(
                async token =>
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(10),
                        token);

                    return 42;
                });

        await act.Should()
            .ThrowAsync<ResilienceTimeoutException>();
    }

    [Fact]
    public async Task AddRetry_ShouldConfigureRetryPolicy()
    {
        var executionCount = 0;

        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(options =>
            {
                options.MaxRetryAttempts = 2;

                options.Delay =
                    TimeSpan.FromMilliseconds(1);

                options.MinDelay =
                    TimeSpan.FromMilliseconds(1);

                options.MaxDelay =
                    TimeSpan.FromMilliseconds(10);

                options.UseExponentialBackoff = false;

                options.Jitter = JitterMode.None;

                options.ShouldHandle =
                    exception =>
                        exception is IOException;
            })
            .Build();

        var result = await pipeline.ExecuteAsync(
            _ =>
            {
                var attempt =
                    Interlocked.Increment(
                        ref executionCount);

                if (attempt <= 2)
                {
                    throw new IOException();
                }

                return Task.FromResult(42);
            });

        result.Should().Be(42);
        executionCount.Should().Be(3);
    }

    [Fact]
    public async Task AddCircuitBreaker_ShouldConfigureCircuitBreakerPolicy()
    {
        var pipeline = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(options =>
            {
                options.FailureThreshold = 1;

                options.BreakDuration =
                    TimeSpan.FromMinutes(1);

                options.ShouldHandle =
                    exception =>
                        exception is IOException;
            })
            .Build();

        Func<Task> first = () =>
            pipeline.ExecuteAsync<int>(
                _ => throw new IOException());

        await first.Should()
            .ThrowAsync<IOException>();

        Func<Task> second = () =>
            pipeline.ExecuteAsync(
                _ => Task.FromResult(42));

        await second.Should()
            .ThrowAsync<BrokenCircuitException>();
    }

    [Fact]
    public async Task Builder_WhenAllPoliciesConfigured_ShouldBuildWorkingPipeline()
    {
        var executionCount = 0;

        var pipeline = new ResiliencePipelineBuilder()
            .AddTimeout(options =>
            {
                options.Timeout =
                    TimeSpan.FromMilliseconds(50);
            })
            .AddRetry(options =>
            {
                options.MaxRetryAttempts = 2;

                options.Delay =
                    TimeSpan.FromMilliseconds(1);

                options.MinDelay =
                    TimeSpan.FromMilliseconds(1);

                options.MaxDelay =
                    TimeSpan.FromMilliseconds(10);

                options.UseExponentialBackoff = false;

                options.Jitter = JitterMode.None;

                options.ShouldHandle =
                    exception =>
                        exception is ResilienceTimeoutException;
            })
            .AddCircuitBreaker(options =>
            {
                options.FailureThreshold = 1;

                options.BreakDuration =
                    TimeSpan.FromMinutes(1);

                options.ShouldHandle =
                    exception =>
                        exception is ResilienceTimeoutException;
            })
            .Build();

        var result = await pipeline.ExecuteAsync(
            async token =>
            {
                var attempt =
                    Interlocked.Increment(
                        ref executionCount);

                if (attempt == 1)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(10),
                        token);
                }

                return 42;
            });

        result.Should().Be(42);
        executionCount.Should().Be(2);
    }

    [Fact]
    public void AddMethods_ShouldReturnSameBuilderInstance()
    {
        var sut = new ResiliencePipelineBuilder();

        sut.AddTimeout(_ => { })
            .Should()
            .BeSameAs(sut);

        sut.AddRetry(_ => { })
            .Should()
            .BeSameAs(sut);

        sut.AddCircuitBreaker(_ => { })
            .Should()
            .BeSameAs(sut);
    }
}
