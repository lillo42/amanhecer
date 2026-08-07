using System;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Linq;
using Amanhencer.Abstractions;
using Amanhencer.Configurator;
using Amanhencer.ExecutingStrategies;
using Amanhencer.Middlewares;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Amanhencer.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAmanhencer(this IServiceCollection services,
        Action<AmanhencerConfigurator>? configure = null)
    {
        services.TryAddTransient<IProcessor, AmanhencerProcessor>();
        services.TryAddTransient<IHandlerFactory, AmanhencerHandlerFactory>();
        services.TryAddTransient<IMiddlewareFactory, AmanhencerMiddlewareFactory>();
        services.TryAddTransient<IPipelineFactory, AmanhencerPipelineFactory>();
        services.TryAddSingleton<IPipelineContextFactory, AmanhencerPipelineContextFactory>();

        var cfg = new AmanhencerConfigurator(services);
        configure?.Invoke(cfg);

        var routing = cfg.RoutingConfigurators
            .GroupBy(x => x.RoutingKey)
            .ToFrozenDictionary(x => x.Key,
                x => x
                    .Select(y => y.MiddlewareOptions.ToImmutableList())
                    .ToImmutableList());
        
        services.AddSingleton(new AmanhencerPipelineOptions(routing));
        services.TryAddSingleton<IExecutingStrategy>(new SequenceExecutingStrategy());
        return services;
    }
}