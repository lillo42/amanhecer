using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Amanhecer.Configurator;

/// <summary>
/// Configures the messaging gateways, named transformer pipelines and global transformers
/// of an Amanhecer application.
/// </summary>
/// <param name="services">The service collection where gateways and transformers are registered.</param>
public class AmanhecerMessagingConfigurator(IServiceCollection services)
{
    /// <summary>
    /// Gets the service collection where gateways and transformers are registered.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Gets the gateways registered via <see cref="AddGateway"/>.
    /// </summary>
    public List<IGateway> Gateways { get; } = [];

    /// <summary>
    /// Gets the named transformer pipelines, keyed by pipeline name.
    /// </summary>
    public Dictionary<string, IReadOnlyList<AmanhecerTransformerOptions>> TransformerPipeline { get; } = [];

    /// <summary>
    /// Gets the transformers applied to every encode and decode transformer pipeline.
    /// </summary>
    public List<AmanhecerTransformerOptions> GlobalTransformers { get; } = [];

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    private Type? _defaultMessageMapperType;

    /// <summary>
    /// Sets the default <see cref="IMessageMapper"/> implementation used by the publications and
    /// subscriptions of every gateway added via <see cref="AddGateway"/> that do not configure a
    /// message mapper themselves. The type is registered in the service collection.
    /// </summary>
    /// <param name="messageMapperType">The default message mapper implementation type.</param>
    /// <returns>The current configurator, for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="messageMapperType"/> does
    /// not implement <see cref="IMessageMapper"/>.</exception>
    public AmanhecerMessagingConfigurator DefaultMessageMapper(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type messageMapperType)
    {
        if (!typeof(IMessageMapper).IsAssignableFrom(messageMapperType))
        {
            throw new ArgumentException(
                $"The type '{messageMapperType.FullName}' does not implement IMessageMapper.",
                nameof(messageMapperType));
        }

        _defaultMessageMapperType = messageMapperType;
        Services.TryAddTransient(messageMapperType);
        return this;
    }

    /// <summary>
    /// Sets the default <see cref="IMessageMapper"/> implementation used by the publications and
    /// subscriptions of every gateway added via <see cref="AddGateway"/> that do not configure a
    /// message mapper themselves. The type is registered in the service collection.
    /// </summary>
    /// <typeparam name="TMapper">The default message mapper implementation type.</typeparam>
    /// <returns>The current configurator, for chaining.</returns>
    public AmanhecerMessagingConfigurator DefaultMessageMapper<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TMapper>() where TMapper : IMessageMapper
    {
        return DefaultMessageMapper(typeof(TMapper));
    }

    /// <summary>
    /// Adds a named transformer pipeline, ordering its transformers by
    /// <see cref="AmanhecerTransformerOptions.Order"/>.
    /// </summary>
    /// <param name="pipelineName">The name of the transformer pipeline.</param>
    /// <param name="options">The transformers that compose the pipeline.</param>
    /// <returns>The current configurator, for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a pipeline with the same name has already been added.
    /// </exception>
    public AmanhecerMessagingConfigurator AddTransformerPipeline(
        string pipelineName,
        IReadOnlyList<AmanhecerTransformerOptions> options)
    {
        if (TransformerPipeline.ContainsKey(pipelineName))
        {
            throw new InvalidOperationException(
                $"A transformer pipeline named '{pipelineName}' is already registered; pipeline names must be unique.");
        }

        TransformerPipeline[pipelineName] =
        [
            .. options
                .OrderBy(x => x.Order)
        ];
        return this;
    }

    /// <summary>
    /// Adds a transformer applied to every encode and decode transformer pipeline, on top of
    /// any pipeline-specific transformers. The transformer contributes to the directions it
    /// implements: <see cref="IEncodeTransformer"/> for encode pipelines,
    /// <see cref="IDecodeTransformer"/> for decode pipelines, both for
    /// <see cref="ITransformer"/> implementations.
    /// </summary>
    /// <typeparam name="TTransformer">The transformer implementation type.</typeparam>
    /// <param name="order">The position of the transformer in the pipeline; lower values run first.</param>
    /// <param name="metadata">Optional metadata stored in the pipeline context's
    /// <see cref="Amanhecer.Abstractions.AmanhecerContext.Metadata"/> when the transformer is created.</param>
    /// <returns>The current configurator, for chaining.</returns>
    public AmanhecerMessagingConfigurator AddGlobalTransformer<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TTransformer>(int order = 0, object? metadata = null)
        where TTransformer : class
    {
        return AddGlobalTransformer(typeof(TTransformer), order, metadata);
    }

    /// <summary>
    /// Adds a transformer applied to every encode and decode transformer pipeline, on top of
    /// any pipeline-specific transformers. The transformer contributes to the directions it
    /// implements: <see cref="IEncodeTransformer"/> for encode pipelines,
    /// <see cref="IDecodeTransformer"/> for decode pipelines, both for
    /// <see cref="ITransformer"/> implementations.
    /// </summary>
    /// <param name="transformerType">The transformer implementation type.</param>
    /// <param name="order">The position of the transformer in the pipeline; lower values run first.</param>
    /// <param name="metadata">Optional metadata stored in the pipeline context's
    /// <see cref="Amanhecer.Abstractions.AmanhecerContext.Metadata"/> when the transformer is created.</param>
    /// <returns>The current configurator, for chaining.</returns>
    public AmanhecerMessagingConfigurator AddGlobalTransformer(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type transformerType, int order = 0, object? metadata = null)
    {
        if (!typeof(IEncodeTransformer).IsAssignableFrom(transformerType) &&
            !typeof(IDecodeTransformer).IsAssignableFrom(transformerType))
        {
            throw new ArgumentException(
                $"The type '{transformerType.FullName}' implements neither IEncodeTransformer nor IDecodeTransformer.",
                nameof(transformerType));
        }

        GlobalTransformers.Add(new AmanhecerTransformerOptions(transformerType, order, metadata));
        Services.TryAddTransient(transformerType);
        return this;
    }

    /// <summary>
    /// Adds the gateway to <see cref="Gateways"/> and registers it as a singleton in
    /// <see cref="Services"/>. Registration performs no broker I/O: the gateway's provisioner
    /// (see <see cref="IGateway.ProvisionerAsync"/>) runs later, when the gateway is first used
    /// (consumers starting or the first publish). Publications and subscriptions without a
    /// message mapper fall back to the default configured via
    /// <see cref="DefaultMessageMapper(Type)"/>, and every configured message mapper type is
    /// registered in <see cref="Services"/> so it can be resolved at runtime.
    /// </summary>
    /// <param name="gateway">The gateway to register.</param>
    /// <returns>The current configurator, for chaining.</returns>
    public AmanhecerMessagingConfigurator AddGateway(IGateway gateway)
    {
        ApplyDefaultMessageMapper(gateway);

        foreach (var publication in gateway.Publications)
        {
            if (publication.MessageMapperType != null)
            {
                Services.TryAddTransient(publication.MessageMapperType);
            }
        }

        foreach (var subscription in gateway.Subscriptions)
        {
            if (subscription.MessageMapperType != null)
            {
                Services.TryAddTransient(subscription.MessageMapperType);
            }
        }

        Gateways.Add(gateway);
        // Factory registration (rather than the pre-built instance) so the container disposes
        // the gateway when it is disposed, and so the gateway gets the application's logger
        // factory when it supports logging.
        Services.AddSingleton<IGateway>(provider =>
        {
            if (gateway is ILoggerFactorySupport logging)
            {
                logging.LoggerFactory ??= provider.GetService<ILoggerFactory>();
            }

            return gateway;
        });
        return this;
    }

    private void ApplyDefaultMessageMapper(IGateway gateway)
    {
        if (_defaultMessageMapperType is null)
        {
            return;
        }

        foreach (var publication in gateway.Publications)
        {
            publication.MessageMapperType ??= _defaultMessageMapperType;
        }

        foreach (var subscription in gateway.Subscriptions)
        {
            subscription.MessageMapperType ??= _defaultMessageMapperType;
        }
    }
}
