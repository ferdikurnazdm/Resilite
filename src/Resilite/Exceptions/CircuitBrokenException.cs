using System;

namespace Resilite;

public sealed class CircuitBrokenException : InvalidOperationException
{
    public CircuitBrokenException(string message) : base(message) { }

    public CircuitBrokenException(string message, Exception innerException)
        : base(message, innerException) { }
}
