using System;

namespace Resilite;

public sealed class ResilienceTimeoutException : TimeoutException
{
    public ResilienceTimeoutException(string message) : base(message) { }
}
