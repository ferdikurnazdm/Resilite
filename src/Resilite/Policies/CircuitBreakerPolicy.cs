using System;

namespace Resilite;

public enum CircuitState { Closed, Open, HalfOpen }

public sealed partial class CircuitBreakerPolicy
{
    private readonly CircuitBreakerOptions _options;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private CircuitState _state = CircuitState.Closed;
    private DateTime _openedAt = DateTime.MinValue;
    private bool _halfOpenProbeInProgress;
    private int _failureCount = 0;

    public CircuitBreakerPolicy(CircuitBreakerOptions options)
    {
        CircuitBreakerOptionsValidator.Validate(options);

        _options = options;
    }



    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var isProbe = await EnterAsync(
            cancellationToken: cancellationToken);

        try
        {
            var result = await action(cancellationToken);

            await OnSuccessAsync(
                isProbe: isProbe,
                cancellationToken: cancellationToken);

            return result;
        }
        catch (Exception exception)
            when (_options.ShouldHandle(exception))
        {
            await OnFailureAsync(
                isProbe: isProbe,
                cancellationToken: cancellationToken);

            throw;
        }
        finally
        {
            if (isProbe)
            {
                await ReleaseHalfOpenProbeAsync(
                    cancellationToken: cancellationToken);
            }
        }
    }

    private async Task<bool> EnterAsync(
        CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(
            cancellationToken: cancellationToken);

        try
        {
            switch (_state)
            {
                case CircuitState.Closed:
                    return false;

                case CircuitState.Open:
                    HandleOpenState();
                    return true;

                case CircuitState.HalfOpen:
                    HandleHalfOpenState();
                    return true;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported circuit state: {_state}.");
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }
}
