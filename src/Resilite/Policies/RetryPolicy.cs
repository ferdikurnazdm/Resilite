using System;
using System.Net.Sockets;

namespace Resilite;

public sealed partial class RetryPolicy
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
                var delayTime = CalculateDelayTime(attempt);

                await Task.Delay(delayTime, cancellationToken);
            }
        }
    }
}
