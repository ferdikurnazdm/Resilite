using System;
using System.Net.Sockets;

namespace Resilite;

public class RetryPolicy
{
    private readonly int _maxRetryAttempts;
    private readonly TimeSpan _delay;
    private readonly bool _useExponentialBackoff;

    public RetryPolicy(
        int maxRetryAttempts,
        TimeSpan delay,
        bool useExponentialBackoff = true)
    {
        if (delay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delay),
                delay,
                "Delay must be grater then Zero"
            );
        }

        if (maxRetryAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxRetryAttempts),
                maxRetryAttempts,
                "Maximum Retry Attempts must be grater then Zero"
            );
        }

        _delay = delay;

        _maxRetryAttempts = maxRetryAttempts;

        _useExponentialBackoff = useExponentialBackoff;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        int attempt = 0;

        while (true)
        {
            attempt++;

            try
            {
                return await action(cancellationToken);
            }
            catch (Exception ex) when (IsTransient(ex) && attempt <= _maxRetryAttempts)
            {
                var delayTime = CaculateDelayTime(attempt);

                await Task.Delay(delayTime, cancellationToken);
            }
        }
    }


    private TimeSpan CaculateDelayTime(int attempt)
    {
        if (!_useExponentialBackoff)
        {
            return _delay;
        }

        return TimeSpan.FromMilliseconds(
            _delay.TotalMilliseconds * Math.Pow(2, attempt - 1));
    }


    private static bool IsTransient(Exception ex)
    {
        return ex is TimeoutException or 
                     IOException or 
                     SocketException;
    }
}
