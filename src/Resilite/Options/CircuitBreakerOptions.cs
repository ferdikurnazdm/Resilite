using System;

namespace Resilite;

public sealed class CircuitBreakerOptions
{
    public int FailureThreshold { get; set; } = 3;

    public TimeSpan BreakDuration { get; set; }
        = TimeSpan.FromSeconds(30);

    public Func<Exception, bool> ShouldHandle { get; set; }
        = DefaultExceptionPredicates.ShouldHandle;
}


internal static class CircuitBreakerOptionsValidator
{
    public static void Validate(CircuitBreakerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.FailureThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.FailureThreshold),
                options.FailureThreshold,
                "Failure threshold must be greater then Zero");
        }

        if (options.BreakDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.BreakDuration),
                options.BreakDuration,
                "Break Duration must be greater then Zero"
            );
        }

        if (options.ShouldHandle is null)
        {
            throw new ArgumentNullException(
                nameof(options.ShouldHandle));
        }
    }
}