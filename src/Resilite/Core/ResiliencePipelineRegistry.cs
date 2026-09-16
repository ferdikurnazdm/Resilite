using System;
using System.Collections.Concurrent;

namespace Resilite;

public sealed record ResiliencePipelineRegistration(
    string Name,
    IResiliencePipeline Pipeline);

public interface IResiliencePipelineRegistry
{
    IResiliencePipeline Get(string name);
    void Register(
        ResiliencePipelineRegistration registration);
}

public class ResiliencePipelineRegistry : IResiliencePipelineRegistry
{
    private readonly ConcurrentDictionary<string, IResiliencePipeline> _pipelines;

    public ResiliencePipelineRegistry()
    {
        _pipelines = new ConcurrentDictionary<string, IResiliencePipeline>(
            StringComparer.OrdinalIgnoreCase);
    }

    public ResiliencePipelineRegistry(
        IEnumerable<ResiliencePipelineRegistration> registrations)
        : this()
    {
        ArgumentNullException.ThrowIfNull(registrations);

        foreach (var registration in registrations)
        {
            Register(registration);
        }
    }

    public void Register(ResiliencePipelineRegistration registration)
    {
        if (string.IsNullOrWhiteSpace(registration.Name))
            throw new ArgumentException("Pipeline name cannot be empty", nameof(registration.Name));

        ArgumentNullException.ThrowIfNull(registration.Pipeline);

        if (!_pipelines.TryAdd(registration.Name, registration.Pipeline))
        {
            throw new InvalidOperationException(
                $"A resilience pipeline with the name '{registration.Name}' is already registered.");
        }
    }

    public IResiliencePipeline Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Pipeline name cannot be empty", nameof(name));

        if (_pipelines.TryGetValue(name, out var pipeline))
        {
            return pipeline;
        }

        throw new KeyNotFoundException(
            $"No resilience pipeline with the name '{name}' was found.");
    }
}
