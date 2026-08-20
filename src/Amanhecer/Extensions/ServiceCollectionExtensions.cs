using System;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Linq;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Configurator;
using Amanhecer.ExecutingStrategies;
using Amanhecer.Messaging;
using Amanhecer.Middlewares;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Amanhecer.Extensions;

/// <summary>
/// Extension methods to register Amanhecer in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Amanhecer dispatcher and its default services (factories, pipeline
    /// configuration and the default sequential executing strategy).
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">An optional action to configure handlers, routing keys and the executing strategy.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddAmanhecer(this IServiceCollection services,
        Action<AmanhecerConfigurator>? configure = null)
    {
        services.TryAddTransient<IDispatcher, AmanhecerDispatcher>();
        services.TryAddTransient<IHandlerFactory, AmanhecerHandlerFactory>();
        services.TryAddTransient<IMiddlewareFactory, AmanhecerMiddlewareFactory>();
        services.TryAddTransient<IPipelineFactory, AmanhecerPipelineFactory>();
        services.TryAddSingleton<IPipelineContextFactory, AmanhecerPipelineContextFactory>();

        services.TryAddSingleton<AmanhecerTelemetryMiddleware>();
        services.TryAddSingleton<AmanhecerLoggerMiddleware>();

        services.TryAddSingleton<IExecutingStrategy, SequenceExecutingStrategy>();

        services.TryAddSingleton<AmanhecerPipelineContextAccessor>();
        services.TryAddSingleton<IPipelineContextAccessor>(provider =>
            provider.GetRequiredService<AmanhecerPipelineContextAccessor>());

        services.TryAddTransient<ITransformerPipelineFactory, AmanhencerTransformerPipelineFactory>();
        services.TryAddTransient<ITransformerFactory, AmanhecerTransformerFactory>();
        services.TryAddTransient<IMessageMapperFactory, AmanhecerMessageMapperFactory>();

        var cfg = new AmanhecerConfigurator(services);
        configure?.Invoke(cfg);

        var routing = cfg.RoutingConfigurators
            .GroupBy(x => x.RoutingKey)
            .ToFrozenDictionary(x => x.Key,
                x => x
                    .Select(y => y.MiddlewareOptions.ToImmutableList())
                    .ToImmutableList());

        services.AddSingleton(new AmanhecerPipelineOptions(routing));


        services.TryAddSingleton<IProducerFinder>(new AmanhecerProducerFinder(
            cfg
                .Gateways
                .SelectMany(x => x.CreateProducers())
                .ToFrozenDictionary(x => x.Key, x => x.Value)));

        services.TryAddSingleton<IPublicationFinder>(new AmanhecerPublicationFinder(
            cfg
                .Gateways
                .SelectMany(x => x.Publications)
                .ToFrozenDictionary(x => x.RoutingKey, x => x)
        ));

        return services;
    }
}