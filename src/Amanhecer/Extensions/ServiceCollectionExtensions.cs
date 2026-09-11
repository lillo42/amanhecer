using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Configurator;
using Amanhecer.ExecutingStrategies;
using Amanhecer.Messaging;
using Amanhecer.Messaging.Handlers;
using Amanhecer.Messaging.Middlewares;
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

        services.TryAddSingleton<AmanhecerTelemetryMiddleware>();
        services.TryAddSingleton<AmanhecerLoggerMiddleware>();

        services.TryAddSingleton<IExecutingStrategy, SequenceExecutingStrategy>();

        services.TryAddSingleton<AmanhecerPipelineContextAccessor>();
        services.TryAddSingleton<IPipelineContextAccessor>(provider =>
            provider.GetRequiredService<AmanhecerPipelineContextAccessor>());

        services.TryAddTransient<IEncodeTransformerPipelineFactory, AmanhecerEncodeTransformerPipelineFactory>();
        services.TryAddTransient<IDecodeTransformerPipelineFactory, AmanhecerDecodeTransformerPipelineFactory>();
        services.TryAddTransient<IEncodeTransformerFactory, AmanhecerEncodeTransformerFactory>();
        services.TryAddTransient<IDecodeTransformerFactory, AmanhecerDecodeTransformerFactory>();
        services.TryAddTransient<IMessageMapperFactory, AmanhecerMessageMapperFactory>();
        services.TryAddTransient<IMessagePump, AmanhecerMessagePump>();
        services.TryAddSingleton<IMessagePumpFactory, AmanhecerMessagePumpFactory>();
        services.TryAddTransient<EncodeMiddleware>();
        services.TryAddTransient<DecodeMiddleware>();
        services.TryAddTransient<JsonMessageMapper>();

        var cfg = new AmanhecerConfigurator(services);
        configure?.Invoke(cfg);

        // Publications and subscriptions without a message mapper fall back to the JSON mapper.
        foreach (var gateway in cfg.Gateways)
        {
            foreach (var publication in gateway.Publications)
            {
                publication.MessageMapperType ??= typeof(JsonMessageMapper);
            }

            foreach (var subscription in gateway.Subscriptions)
            {
                subscription.MessageMapperType ??= typeof(JsonMessageMapper);
            }
        }

        cfg.AddRoutingKey("Amanhecer.Messaging.Post", routing => routing.UseHandler<PostMessageHandler>());

        // Publication routing keys resolve the producer and the publication, so duplicates
        // across gateways would make the finders ambiguous.
        var duplicatedRoutingKey = cfg.Gateways
            .SelectMany(x => x.Publications)
            .GroupBy(x => x.RoutingKey)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .FirstOrDefault();

        if (duplicatedRoutingKey != null)
        {
            throw new InvalidOperationException(
                $"The publication routing key '{duplicatedRoutingKey}' is declared more than once. " +
                "Publication routing keys must be unique across all gateways.");
        }

        var routing = cfg.RoutingConfigurators
            .GroupBy(x => x.RoutingKey)
            .ToFrozenDictionary(x => x.Key,
                x => x
                    .Select(y => y.Middlewares.ToList().AsEnumerable())
                    .ToList());

        services.TryAddSingleton(new AmanhecerPipelineOptions(routing));

        // Producers are created on first resolution (the first publish), so building the
        // service collection opens no broker connection. The gateways are resolved through
        // the provider so their registrations (singleton lifetime, logger factory) apply.
        services.TryAddSingleton<IProducerFinder>(provider => new AmanhecerProducerFinder(
            provider
                .GetServices<IGateway>()
                .SelectMany(x => x.CreateProducers())
                .ToFrozenDictionary(x => x.Key, x => x.Value)));

        services.TryAddSingleton<IPublicationFinder>(new AmanhecerPublicationFinder(
            cfg
                .Gateways
                .SelectMany(x => x.Publications)
                .ToFrozenDictionary(x => x.RoutingKey, x => x)
        ));

        var transformerPipelines =
            new Dictionary<string, IReadOnlyList<AmanhecerTransformerOptions>>(cfg.TransformerPipelineConfiguration);

        var globalTransformers = cfg.GlobalTransformers.OrderBy(x => x.Order).ToList();
        var mergedPipelines = new HashSet<string>();

        foreach (var publication in cfg.Gateways.SelectMany(x => x.Publications))
        {
            MergeTransformers(
                TransformerPipelineNames.Encode(publication.Name),
                publication.MessageMapperType,
                publication.Transformers);
        }

        foreach (var subscription in cfg.Gateways.SelectMany(x => x.Subscriptions))
        {
            MergeTransformers(
                TransformerPipelineNames.Decode(subscription.Name),
                subscription.MessageMapperType,
                subscription.Transformers);
        }

        // Explicitly named pipelines that belong to no publication or subscription still
        // get the global transformers.
        if (globalTransformers.Count > 0)
        {
            foreach (var pipelineName in transformerPipelines.Keys.ToList())
            {
                if (mergedPipelines.Add(pipelineName))
                {
                    transformerPipelines[pipelineName] =
                        [.. globalTransformers.Concat(transformerPipelines[pipelineName]).OrderBy(x => x.Order)];
                }
            }
        }

        services.TryAddSingleton(new AmanhecerTransformerPipelineOptions(
            transformerPipelines.ToFrozenDictionary()));

        return services;

        void MergeTransformers(
            string pipelineName,
            Type? messageMapperType,
            IReadOnlyList<AmanhecerTransformerOptions> configured)
        {
            mergedPipelines.Add(pipelineName);

            var discovered = messageMapperType?
                .GetCustomAttributes<TransformerAttribute>()
                .Select(x => new AmanhecerTransformerOptions(x.GetTransformerType(), x.Order, x))
                ?? [];

            var transformers = globalTransformers.Concat(discovered).Concat(configured).ToList();
            if (transformers.Count == 0)
            {
                return;
            }

            foreach (var transformer in transformers)
            {
                var transformerType = transformer.TransformerType;
                if (!typeof(IEncodeTransformer).IsAssignableFrom(transformerType) &&
                    !typeof(IDecodeTransformer).IsAssignableFrom(transformerType))
                {
                    throw new InvalidOperationException(
                        $"The transformer type '{transformerType.FullName}' implements neither " +
                        "IEncodeTransformer nor IDecodeTransformer.");
                }

                services.TryAddTransient(transformerType);
            }

            transformerPipelines[pipelineName] = transformerPipelines.TryGetValue(pipelineName, out var existing)
                ? [.. existing.Concat(transformers).OrderBy(x => x.Order)]
                : [.. transformers.OrderBy(x => x.Order)];
        }
    }
}