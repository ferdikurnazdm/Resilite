using System;

namespace Resilite;

public enum JitterMode { None, Equal, Full }

public sealed class RetryOptions
{
    public int MaxRetryAttempts { get; set; } = 3;

    public TimeSpan Delay { get; set; }
        = TimeSpan.FromSeconds(1);

    public TimeSpan MaxDelay { get; set; } 
        = TimeSpan.FromSeconds(30);

    public TimeSpan MinDelay { get; set; } 
        = TimeSpan.FromMilliseconds(20);

    public JitterMode Jitter { get; set; } = JitterMode.Equal;

    public bool UseExponentialBackoff { get; set; } = true;

    public bool UseJitter { get; set; } = true;

    public Func<Exception, bool> ShouldHandle { get; set; }
        = DefaultExceptionPredicates.ShouldHandle;
}


public static class RetryOptionsValidator
{
    public static void Validate(RetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxRetryAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.MaxRetryAttempts),
                options.MaxRetryAttempts,
                "Maximum retry attempts must be greater than zero.");
        }

        if (options.Delay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.Delay),
                options.Delay,
                "Delay must be greater than zero.");
        }

        if (options.MaxDelay < options.Delay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.MaxDelay),
                options.MaxDelay,
                "MaxDelay must be greater than or equal to Delay.");
        }

        ArgumentNullException.ThrowIfNull(options.ShouldHandle);
    }
}