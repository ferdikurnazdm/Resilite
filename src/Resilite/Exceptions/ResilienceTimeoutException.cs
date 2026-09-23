using System;

namespace Resilite;

public sealed class ResilienceTimeoutException : TimeoutException
{
    public TimeSpan Timeout { get; }

    public string? PipelineName { get; }

    public ResilienceTimeoutException(
        TimeSpan timeout,
        string? pipelineName = null,
        Exception? innerException = null)
        : base(CreateMessage(timeout, pipelineName), innerException)
    {
        Timeout = timeout;
        PipelineName = pipelineName;
    }

    private static string CreateMessage(
        TimeSpan timeout,
        string? pipelineName)
    {
        return pipelineName is null
            ? $"The operation exceeded the configured timeout of {timeout}."
            : $"The operation in pipeline '{pipelineName}' exceeded the configured timeout of {timeout}.";
    }
}