using System;
using FluentAssertions;

namespace Resilite.UnitTest;

public class CircuitBreakerPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_WhenFailuresExceedThreshold_ShouldBreakCircuit()
    {
        // Arrange
        var policy = new CircuitBreakerPolicy(failureThreshold: 2, breakDuration: TimeSpan.FromSeconds(5));

        // Act & Assert 1. Hata
        Func<Task> act1 = async () => await policy.ExecuteAsync<string>(async ct => throw new IOException("Hata 1"), CancellationToken.None);
        await act1.Should().ThrowAsync<IOException>();

        // Act & Assert 2. Hata (Eşik sınıra ulaşıldı, devre açılmalı)
        Func<Task> act2 = async () => await policy.ExecuteAsync<string>(async ct => throw new IOException("Hata 2"), CancellationToken.None);
        await act2.Should().ThrowAsync<IOException>();

        // Act & Assert 3. Deneme: Devre açık olduğu için fonksiyona hiç girmeden CircuitBrokenException fırlatmalı
        Func<Task> act3 = async () => await policy.ExecuteAsync(async ct => Task.FromResult("Çalışmamalı"), CancellationToken.None);

        await act3.Should().ThrowAsync<CircuitBrokenException>()
                 .WithMessage("*Devre AÇIK*");
    }
}
