using System;

namespace Resilite;

public sealed partial class CircuitBreakerPolicy
{
    private void MoveToHalfOpen()
    {
        _state = CircuitState.HalfOpen;

        _halfOpenProbeInProgress = true;
    }

    private void OpenCircuit()
    {
        _state = CircuitState.Open;

        _openedAt = DateTime.UtcNow;

        _halfOpenProbeInProgress = false;
    }

    private void CloseCircuit()
    {
        _state = CircuitState.Closed;

        _failureCount = 0;

        _halfOpenProbeInProgress = false;

        _openedAt = DateTime.MinValue;
    }

    private async Task OnSuccessAsync(
        bool isProbe, 
        CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);

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

    private async Task OnFailureAsync(
        bool isProbe,
        CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);

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
}
