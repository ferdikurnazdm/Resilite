using System;
using System.Net.Sockets;

namespace Resilite;

public enum CircuitState { Closed, Open, HalfOpen }

public sealed class CircuitBreakerPolicy
{
    private readonly CircuitBreakerOptions _options;


    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private CircuitState _state = CircuitState.Closed;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private bool _halfOpenProbeInProgress;
    private int _failureCount = 0;

    public CircuitBreakerPolicy(CircuitBreakerOptions options)
    {
        CircuitBreakerOptionsValidator.Validate(options);

        _options = options;
    }



    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        await EnsureExecutionAllowedAsync();

        try
        {
            var result = await action(cancellationToken);

            await HandleSuccessAsync();

            return result;
        }
        catch (Exception exception)
            when (_options.ShouldHandle(exception))
        {
            await HandleFailureAsync();

            throw;
        }
    }

    private async Task EnsureExecutionAllowedAsync()
    {
        await _stateLock.WaitAsync();

        try
        {
            switch (_state)
            {
                case CircuitState.Closed:
                    return;

                case CircuitState.Open:
                    HandleOpenState();
                    return;

                case CircuitState.HalfOpen:
                    HandleHalfOpenState();
                    return;

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

    private void HandleOpenState()
    {
        if (!HasBreakDurationElapsed())
        {
            throw new CircuitBrokenException(
                "Circuit is open. The request was blocked.");
        }

        MoveToHalfOpen();
    }

    private void HandleHalfOpenState()
    {
        if (_halfOpenProbeInProgress)
        {
            throw new CircuitBrokenException(
                "Circuit is half-open. A probe request is already in progress.");
        }

        _halfOpenProbeInProgress = true;
    }

    private bool HasBreakDurationElapsed()
    {
        return DateTime.UtcNow - _lastFailureTime >= _options.BreakDuration;
    }

    private void MoveToHalfOpen()
    {
        _state = CircuitState.HalfOpen;

        _halfOpenProbeInProgress = true;
    }

    private async Task HandleFailureAsync()
    {
        await _stateLock.WaitAsync();

        try
        {
            _failureCount++;

            _lastFailureTime = DateTime.UtcNow;

            if (ShouldOpenCircuit())
            {
                OpenCircuit();
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private bool ShouldOpenCircuit()
    {
        return _state == CircuitState.HalfOpen
            || _failureCount >= _options.FailureThreshold;
    }

    private void OpenCircuit()
    {
        _state = CircuitState.Open;
        _halfOpenProbeInProgress = false;
    }


    private async Task HandleSuccessAsync()
    {
        await _stateLock.WaitAsync();

        try
        {
            CloseCircuit();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private void CloseCircuit()
    {
        _state = CircuitState.Closed;
        _failureCount = 0;
        _halfOpenProbeInProgress = false;
    }
}
