using System;
using FluentAssertions;
using NSubstitute;

namespace Resilite.UnitTest;

public class RetryPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_WhenFailsThenSucceeds_ShouldRetryAndSucceed()
    {
        // Arrange
        var retryOptions = new RetryOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(10),
            UseExponentialBackoff = false,
        };

        var policy = new RetryPolicy(retryOptions);

        var unstableService = Substitute.For<IAsyncActionService>();

        unstableService.InvokeAsync(Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new IOException("Geçici hata 1"),
                _ => throw new IOException("Geçici hata 2"),
                _ => Task.FromResult("Başarılı Sonuç")
            );

        // Act
        var result = await policy.ExecuteAsync(async ct => await unstableService.InvokeAsync(ct), CancellationToken.None);

        // Assert
        result.Should().Be("Başarılı Sonuç");

        await unstableService.Received(3).InvokeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenExceedsMaxRetries_ShouldThrowOriginalException()
    {
        // Arrange
        var retryOptions = new RetryOptions
        {
            MaxRetryAttempts = 2,
            Delay = TimeSpan.FromMilliseconds(10),
            UseExponentialBackoff = false,
        };

        var policy = new RetryPolicy(retryOptions);

        // Act
        Func<Task> act = async () => await policy.ExecuteAsync<object>(async ct =>
        {
            throw new IOException("Kalıcı Bağlantı Hatası");
        }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<IOException>()
                 .WithMessage("Kalıcı Bağlantı Hatası");
    }
}

// Test amaçlı arayüz tanımı
public interface IAsyncActionService
{
    Task<string> InvokeAsync(CancellationToken cancellationToken);
}
