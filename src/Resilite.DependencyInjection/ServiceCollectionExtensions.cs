using System;
using Microsoft.Extensions.DependencyInjection;

namespace Resilite.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddResiliteRegistry(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IResiliencePipelineRegistry, ResiliencePipelineRegistry>();

        return services;
    }

    public static IServiceCollection AddResilitePipeline(
        this IServiceCollection services,
        string name,
        Action<ResiliencePipelineBuilder> configureBuilder)
    {
        ArgumentNullException.ThrowIfNull(services);

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        ArgumentNullException.ThrowIfNull(configureBuilder);

        var builder = new ResiliencePipelineBuilder();

        configureBuilder(builder);

        var pipeline = builder.Build();

        services.AddSingleton(
            new ResiliencePipelineRegistration(
                name,
                pipeline));

        return services;
    }

    public static IServiceCollection AddResilitePipeline(
        this IServiceCollection services,
        Action<ResiliencePipelineBuilder> configureBuilder)
    {
        return services.AddResilitePipeline(
            "Default",
            configureBuilder);
    }
}
