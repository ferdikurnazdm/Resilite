using System;
using System.Net.Sockets;

namespace Resilite;

public enum CircuitState { Closed, Open, HalfOpen }

public sealed class CircuitBreakerPolicy
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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var isProbe = await EnterAsync();

        try
        {
            var result = await action(cancellationToken);

            await OnSuccessAsync(isProbe);

            return result;
        }
        catch (Exception exception)
            when (_options.ShouldHandle(exception))
        {
            await OnFailureAsync(isProbe);

            throw;
        }
        finally
        {
            if (isProbe)
            {
                await ReleaseHalfOpenProbeAsync();
            }
        }
    }

    private async Task<bool> EnterAsync()
    {
        await _stateLock.WaitAsync();

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

    private void HandleOpenState()
    {
        if (!CanTryAgain())
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

    private bool CanTryAgain()
    {
        return DateTime.UtcNow - _openedAt >= _options.BreakDuration;
    }

    private void MoveToHalfOpen()
    {
        _state = CircuitState.HalfOpen;

        _halfOpenProbeInProgress = true;
    }

    private async Task OnFailureAsync(bool isProbe)
    {
        await _stateLock.WaitAsync();

        try
        {
            if (isProbe)
            {
                HandleProbeFailure();

                return;
            }

            HandleNormalFailure();
        }
        finally
        {
            _stateLock.Release();
        }
    }


    private void HandleProbeFailure()
    {
        if (_state != CircuitState.HalfOpen)
        {
            return;
        }

        OpenCircuit();
    }

    private void HandleNormalFailure()
    {
        if (_state != CircuitState.Closed)
        {
            return;
        }

        _failureCount++;

        if (ShouldOpenCircuit())
        {
            OpenCircuit();
        }
    }


    private bool ShouldOpenCircuit()
    {
        return _failureCount >= _options.FailureThreshold;
    }

    private void OpenCircuit()
    {
        _state = CircuitState.Open;

        _openedAt = DateTime.UtcNow;

        _halfOpenProbeInProgress = false;
    }


    private async Task OnSuccessAsync(bool isProbe)
    {
        await _stateLock.WaitAsync();

        try
        {
            if (isProbe)
            {
                HandleProbeSuccess();
                return;
            }

            HandleNormalSuccess();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private void HandleProbeSuccess()
    {
        if (_state != CircuitState.HalfOpen)
        {
            return;
        }

        CloseCircuit();
    }

    private void HandleNormalSuccess()
    {
        if (_state != CircuitState.Closed)
        {
            return;
        }

        _failureCount = 0;
    }

    private void CloseCircuit()
    {
        _state = CircuitState.Closed;

        _failureCount = 0;

        _halfOpenProbeInProgress = false;

        _openedAt = DateTime.MinValue;
    }

    private async Task ReleaseHalfOpenProbeAsync()
    {
        await _stateLock.WaitAsync();

        try
        {
            if (_state == CircuitState.HalfOpen && _halfOpenProbeInProgress)
            {
                _halfOpenProbeInProgress = false;
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }
}
