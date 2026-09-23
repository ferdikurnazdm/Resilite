using System;

namespace Resilite;

public sealed partial class CircuitBreakerPolicy
{
    private void HandleOpenState()
    {
        if (!CanTryAgain())
        {
            throw new BrokenCircuitException(
                "Circuit is open. The request was blocked.");
        }

        MoveToHalfOpen();
    }

    private bool CanTryAgain()
    {
        return DateTime.UtcNow - _openedAt >= _options.BreakDuration;
    }
}
