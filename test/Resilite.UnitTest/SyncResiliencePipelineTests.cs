// using System;
// using FluentAssertions;

// namespace Resilite.UnitTest;

// public class SyncResiliencePipelineTests
// {
//     [Fact]
//     public void Execute_WhenSyncOperationSucceeds_ShouldReturnResult()
//     {
//         // Arrange
//         var pipeline = new ResiliencePipelineBuilder()
//             .AddRetry(maxRetryAttempts: 2, delay: TimeSpan.FromMilliseconds(10))
//             .Build();

//         // Act
//         var result = pipeline.Execute(() => "Senkron Başarılı");

//         // Assert
//         result.Should().Be("Senkron Başarılı");
//     }

//     [Fact]
//     public void Execute_WhenSyncOperationFailsExceedingRetries_ShouldThrowException()
//     {
//         // Arrange
//         var pipeline = new ResiliencePipelineBuilder()
//             .AddRetry(maxRetryAttempts: 2, delay: TimeSpan.FromMilliseconds(10))
//             .Build();

//         int attemptCount = 0;

//         // Act
//         Action act = () => pipeline.Execute<string>(() =>
//         {
//             attemptCount++;
//             throw new IOException("Senkron G/Ç Hatası");
//         });

//         // Assert
//         act.Should().Throw<IOException>()
//            .WithMessage("Senkron G/Ç Hatası");

//         attemptCount.Should().Be(3); // İlk deneme + 2 retry = 3 toplam deneme
//     }
// }
