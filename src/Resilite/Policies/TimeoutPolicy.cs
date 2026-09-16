using System;

namespace Resilite;

public class TimeoutPolicy
{
    private readonly TimeSpan _timeout;

    public TimeoutPolicy(TimeSpan timeout)
    {
        if(timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "Timeout must be grater then Zero"
            );
        }

        _timeout = timeout;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action, 
        CancellationToken externalToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var cancellationTokenSource = CancellationTokenSource
            .CreateLinkedTokenSource(externalToken);
     
        cancellationTokenSource.CancelAfter(_timeout);

        try
        {
            return await action(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (!externalToken.IsCancellationRequested)
        {
            throw new ResilienceTimeoutException(
                $"The operation exceeded the configured timeout period ({_timeout.TotalMilliseconds}ms).");
        }
    }
}
