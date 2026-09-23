using System;

namespace Resilite;

public sealed class TimeoutPolicy
{
    private readonly TimeoutOptions _options;

    public TimeoutPolicy(TimeoutOptions options)
    {
        TimeoutOptionsValidator.Validate(options);

        _options = options;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action, 
        CancellationToken externalToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var cancellationTokenSource = CancellationTokenSource
            .CreateLinkedTokenSource(externalToken);
     
        cancellationTokenSource.CancelAfter(_options.Timeout);

        try
        {
            return await action(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException ex) when (!externalToken.IsCancellationRequested)
        {
            throw new ResilienceTimeoutException(
                _options.Timeout,
                innerException: ex);
        }
    }
}
