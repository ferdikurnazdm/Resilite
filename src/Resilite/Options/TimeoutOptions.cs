using System;

namespace Resilite;

public sealed class TimeoutOptions
{
    public TimeSpan Timeout { get; set; }
        = TimeSpan.FromSeconds(30);
}

public static class TimeoutOptionsValidator
{
    public static void Validate(TimeoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.Timeout),
                options.Timeout,
                "Timeout must be greater than zero.");
        }
    }
}