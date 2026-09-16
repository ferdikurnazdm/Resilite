using System;
using FluentAssertions;

namespace Resilite.UnitTest;

public class TimeoutPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_WhenOperationCompletesInTime_ShouldReturnResult()
    {
        // Arrange
        var policy = new TimeoutPolicy(TimeSpan.FromSeconds(2));

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
        var policy = new TimeoutPolicy(TimeSpan.FromMilliseconds(50));

        // Act
        Func<Task> act = async () => await policy.ExecuteAsync(async ct =>
        {
            await Task.Delay(300, ct);
            return "Başarısız";
        }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ResilienceTimeoutException>()
                 .WithMessage("*zaman aşımı süresini*");
    }
}
