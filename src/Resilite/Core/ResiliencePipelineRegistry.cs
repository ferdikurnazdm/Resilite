using System;
using System.Collections.Concurrent;

namespace Resilite;

public interface IResiliencePipelineRegistry
{
    void Register(
        string name, 
        IResiliencePipeline pipeline);

    IResiliencePipeline Get(string name);
}

public class ResiliencePipelineRegistry : IResiliencePipelineRegistry
{
    private readonly ConcurrentDictionary<string, IResiliencePipeline> _pipelines = 
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(string name, IResiliencePipeline pipeline)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Pipeline name cannot be empty", nameof(name));

        ArgumentNullException.ThrowIfNull(pipeline);

        if (!_pipelines.TryAdd(name, pipeline))
        {
            throw new InvalidOperationException(
                $"A resilience pipeline with the name '{name}' is already registered.");
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
