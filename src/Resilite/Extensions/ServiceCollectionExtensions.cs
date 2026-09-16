using System;
using Microsoft.Extensions.DependencyInjection;

namespace Resilite;

public static class ServiceCollectionExtensions
{
    private static readonly ResiliencePipelineRegistry _globalRegistry = new();

    public static IServiceCollection AddResilitePipeline(
        this IServiceCollection services,
        string name,
        Action<ResiliencePipelineBuilder> configureBuilder)
    {
        if (configureBuilder == null) throw new ArgumentNullException(nameof(configureBuilder));

        var builder = new ResiliencePipelineBuilder();

        configureBuilder(builder);

        var pipeline = builder.Build();

        _globalRegistry.Register(name, pipeline);

        services.AddSingleton<IResiliencePipelineRegistry>(_globalRegistry);

        if (name.Equals("Default", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IResiliencePipeline>(pipeline);
        }

        return services;
    }

    public static IServiceCollection AddResilitePipeline(
        this IServiceCollection services,
        Action<ResiliencePipelineBuilder> configureBuilder)
    {
        return services.AddResilitePipeline("Default", configureBuilder);
    }
}
