using System;

namespace Resilite;

public sealed class ResiliencePipeline : IResiliencePipeline
{
    private readonly TimeoutPolicy? _timeoutPolicy;
    private readonly RetryPolicy? _retryPolicy;
    private readonly CircuitBreakerPolicy? _circuitBreakerPolicy;

    public ResiliencePipeline(
        TimeoutPolicy? timeoutPolicy = null,
        RetryPolicy? retryPolicy = null,
        CircuitBreakerPolicy? circuitBreakerPolicy = null)
    {
        _timeoutPolicy = timeoutPolicy;

        _retryPolicy = retryPolicy;
        
        _circuitBreakerPolicy = circuitBreakerPolicy;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action, 
        CancellationToken cancellationToken = default)
    {
        Func<CancellationToken, Task<T>> currentAction = async (ct) =>
        {
            if (_timeoutPolicy is not null)
            {
                return await _timeoutPolicy.ExecuteAsync(action, ct);
            }

            return await action(ct);
        };

        if (_retryPolicy is not null)
        {
            var nextAction = currentAction;

            currentAction = async (ct) => await _retryPolicy.ExecuteAsync(nextAction, ct);
        }

        if (_circuitBreakerPolicy is not null)
        {
            var nextAction = currentAction;

            currentAction = async (ct) => await _circuitBreakerPolicy.ExecuteAsync(nextAction, ct);
        }

        return await currentAction(cancellationToken);
    }

    public async Task ExecuteAsync(
        Func<CancellationToken, Task> action, 
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async ct =>
        {
            await action(ct);

            return true;
            
        }, cancellationToken);
    }
}
