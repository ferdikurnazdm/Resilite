using System;

namespace Resilite;

public sealed partial class CircuitBreakerPolicy
{
    private void HandleHalfOpenState()
    {
        if (_halfOpenProbeInProgress)
        {
            throw new BrokenCircuitException(
                "Circuit is half-open. A probe request is already in progress.");
        }

        _halfOpenProbeInProgress = true;
    }

    private void HandleProbeFailure()
    {
        if (_state != CircuitState.HalfOpen)
        {
            return;
        }

        OpenCircuit();
    }

    private void HandleProbeSuccess()
    {
        if (_state != CircuitState.HalfOpen)
        {
            return;
        }

        CloseCircuit();
    }

    private async Task ReleaseHalfOpenProbeAsync(
        CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);

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
