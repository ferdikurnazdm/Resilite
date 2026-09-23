using System;

namespace Resilite;

public sealed partial class CircuitBreakerPolicy
{
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

    private void HandleNormalSuccess()
    {
        if (_state != CircuitState.Closed)
        {
            return;
        }

        _failureCount = 0;
    }
}
