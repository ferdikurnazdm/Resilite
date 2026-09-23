using System;

namespace Resilite;

public sealed partial class RetryPolicy
{
    private TimeSpan CalculateDelayTime(int attempt)
    {
        var baseMilliseconds = CalculateBackoffMilliseconds(attempt);

        var delayMilliseconds = _options.Jitter switch
        {
            JitterMode.None => baseMilliseconds,

            JitterMode.Equal => baseMilliseconds * (0.8 + Random.Shared.NextDouble() * 0.4),

            JitterMode.Full => Random.Shared.NextDouble() * baseMilliseconds,

            _ => baseMilliseconds
        };

        delayMilliseconds = Math.Clamp(
            delayMilliseconds,
            _options.MinDelay.TotalMilliseconds,
            _options.MaxDelay.TotalMilliseconds);

        return TimeSpan.FromMilliseconds(delayMilliseconds);
    }

    private double CalculateBackoffMilliseconds(int attempt)
    {
        if (!_options.UseExponentialBackoff)
        {
            return _options.Delay.TotalMilliseconds;
        }

        var exponent = attempt - 1;

        var multiplier = Math.Pow(2, exponent);

        return _options.Delay.TotalMilliseconds * multiplier;
    }
}
