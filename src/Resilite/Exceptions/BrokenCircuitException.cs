using System;

namespace Resilite;

public sealed class BrokenCircuitException : InvalidOperationException
{
    public BrokenCircuitException(string message) : base(message) { }

    public BrokenCircuitException(string message, Exception innerException)
        : base(message, innerException) { }
}
