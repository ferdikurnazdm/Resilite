using System;
using System.Net.Sockets;

namespace Resilite;

public enum CircuitState { Closed, Open, HalfOpen }

public sealed class CircuitBreakerPolicy
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _breakDuration;
    private CircuitState _state = CircuitState.Closed;
    private int _failureCount = 0;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _halfOpenProbeInProgress;

    public CircuitBreakerPolicy(
        int failureThreshold,
        TimeSpan breakDuration)
    {
        if (failureThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureThreshold),
                failureThreshold,
                "Failure threshold must be greater then Zero");
        }

        if (breakDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(breakDuration),
                breakDuration,
                "Break Duration must be greater then Zero"
            );
        }

        _failureThreshold = failureThreshold;

        _breakDuration = breakDuration;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        await CheckCircuitStateAsync();

        try
        {
            var result = await action(cancellationToken);

            await ResetAsync();

            return result;
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            await RecordFailureAsync();

            throw;
        }
    }

    private async Task CheckCircuitStateAsync()
    {
        await _lock.WaitAsync();

        try
        {
            if (_state == CircuitState.Closed)
                return;

            if (_state == CircuitState.Open)
            {
                if (DateTime.UtcNow - _lastFailureTime <= _breakDuration)
                {
                    throw new CircuitBrokenException(
                        "Circuit is OPEN. The device is in protection mode and the request was blocked.");
                }

                _state = CircuitState.HalfOpen;

                _halfOpenProbeInProgress = true;

                return;
            }

            if (_state == CircuitState.HalfOpen)
            {
                if (_halfOpenProbeInProgress)
                {
                    throw new CircuitBrokenException(
                        "Circuit is HALF-OPEN. A probe request is already in progress.");
                }

                _halfOpenProbeInProgress = true;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task RecordFailureAsync()
    {
        await _lock.WaitAsync();

        try
        {
            _failureCount++;

            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount >= _failureThreshold || _state == CircuitState.HalfOpen)
            {
                _state = CircuitState.Open;

                _halfOpenProbeInProgress = false;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task ResetAsync()
    {
        await _lock.WaitAsync();

        try
        {
            _state = CircuitState.Closed;

            _failureCount = 0;

            _halfOpenProbeInProgress = false;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static bool IsTransient(Exception ex)
    {
        return ex is TimeoutException
            or IOException
            or SocketException;
    }
}
