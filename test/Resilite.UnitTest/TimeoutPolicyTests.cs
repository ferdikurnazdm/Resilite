using System;
using FluentAssertions;

namespace Resilite.UnitTest;

public class TimeoutPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_WhenOperationCompletesInTime_ShouldReturnResult()
    {
        // Arrange
        var timeoutOptions = new TimeoutOptions
        {
            Timeout = TimeSpan.FromSeconds(2)
        };

        var policy = new TimeoutPolicy(timeoutOptions);

        // Act
        var result = await policy.ExecuteAsync(async ct =>
        {
            await Task.Delay(100, ct);
            return "Başarılı";
        }, CancellationToken.None);

        // Assert
        result.Should().Be("Başarılı");
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationExceedsTimeout_ShouldThrowResilienceTimeoutException()
    {
        // Arrange
        var timeoutOptions = new TimeoutOptions
        {
            Timeout = TimeSpan.FromMilliseconds(50)
        };

        var policy = new TimeoutPolicy(timeoutOptions);

        // Act
        Func<Task> act = async () => await policy.ExecuteAsync(async ct =>
        {
            await Task.Delay(300, ct);
            return "Başarısız";
        }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ResilienceTimeoutException>()
                 .WithMessage("The operation exceeded the configured timeout period (50ms).");
    }
}
