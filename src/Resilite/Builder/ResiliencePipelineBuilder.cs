using System;

namespace Resilite;

public sealed class ResiliencePipelineBuilder
{
    private TimeoutPolicy? _timeoutPolicy;
    private RetryPolicy? _retryPolicy;
    private CircuitBreakerPolicy? _circuitBreakerPolicy;

    public ResiliencePipelineBuilder AddTimeout(TimeSpan timeout)
    {
        _timeoutPolicy = new TimeoutPolicy(timeout);

        return this;
    }

    public ResiliencePipelineBuilder AddRetry(
        int maxRetryAttempts, 
        TimeSpan delay, 
        bool useExponentialBackoff = true)
    {
        _retryPolicy = new RetryPolicy(
            maxRetryAttempts, 
            delay, 
            useExponentialBackoff);

        return this;
    }

    public ResiliencePipelineBuilder AddCircuitBreaker(
        int failureThreshold, 
        TimeSpan breakDuration)
    {
        _circuitBreakerPolicy = new CircuitBreakerPolicy(
            failureThreshold, 
            breakDuration);

        return this;
    }

    public IResiliencePipeline Build()
    {
        return new ResiliencePipeline(
            _timeoutPolicy, 
            _retryPolicy, 
            _circuitBreakerPolicy);
    }
}
