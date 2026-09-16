using System;
using System.Net.Sockets;

namespace Resilite;

public class RetryPolicy
{
    private readonly RetryOptions _options;

    public RetryPolicy(RetryOptions options)
    {
        RetryOptionsValidator.Validate(options);

        _options = options;
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
            catch (Exception ex) 
                when (_options.ShouldHandle(ex) && attempt <= _options.MaxRetryAttempts)
            {
                var delayTime = CaculateDelayTime(attempt);

                await Task.Delay(delayTime, cancellationToken);
            }
        }
    }


    private TimeSpan CaculateDelayTime(int attempt)
    {
        var delayMilliseconds = _options.UseExponentialBackoff
            ? _options.Delay.TotalMilliseconds * Math.Pow(2, attempt - 1)
            : _options.Delay.TotalMilliseconds;

        if (_options.UseJitter)
        {
            delayMilliseconds *= Random.Shared.NextDouble();
        }

        return TimeSpan.FromMilliseconds(delayMilliseconds);
    }
}
