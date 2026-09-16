using System;

namespace Resilite;

public sealed class ResiliencePipelineBuilder
{
    private TimeoutPolicy? _timeoutPolicy;
    private RetryPolicy? _retryPolicy;
    private CircuitBreakerPolicy? _circuitBreakerPolicy;

    public ResiliencePipelineBuilder AddTimeout(
        Action<TimeoutOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new TimeoutOptions();

        configure(options);

        _timeoutPolicy = new TimeoutPolicy(options);

        return this;
    }

    public ResiliencePipelineBuilder AddRetry(
        Action<RetryOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new RetryOptions();

        configure(options);

        _retryPolicy = new RetryPolicy(options);

        return this;
    }

    public ResiliencePipelineBuilder AddCircuitBreaker(
        Action<CircuitBreakerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new CircuitBreakerOptions();

        configure(options);

        _circuitBreakerPolicy =
            new CircuitBreakerPolicy(options);

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
