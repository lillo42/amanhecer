using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

    /// <summary>
    /// Adds a named transformer pipeline, ordering its transformers by
    /// <see cref="AmanhecerTransformerOptions.Order"/>.
    /// </summary>
    /// <param name="pipelineName">The name of the transformer pipeline.</param>
    /// <param name="options">The transformers that compose the pipeline.</param>
    /// <returns>The current configurator, for chaining.</returns>
    /// <exception cref="NotImplementedException">
    /// Thrown when a pipeline with the same name has already been added.
    /// </exception>
    public AmanhecerMessagingConfigurator AddTransformerPipeline(
        string pipelineName,
        IReadOnlyList<AmanhecerTransformerOptions> options)
    {
        if (TransformerPipeline.ContainsKey(pipelineName))
        {
            throw new NotImplementedException();
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
    /// <param name="metadata">Optional metadata passed to the transformer on initialisation.</param>
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
    /// <param name="metadata">Optional metadata passed to the transformer on initialisation.</param>
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
    /// Runs the gateway's provisioner (see <see cref="IGateway.ProvisionerAsync"/>), then adds the
    /// gateway to <see cref="Gateways"/> and registers it as a singleton in <see cref="Services"/>.
    /// </summary>
    /// <param name="gateway">The gateway to register.</param>
    /// <returns>The current configurator, for chaining.</returns>
    public AmanhecerMessagingConfigurator AddGateway(IGateway gateway)
    {
        gateway.ProvisionerAsync().GetAwaiter().GetResult();

        Gateways.Add(gateway);
        Services.AddSingleton(gateway);
        return this;
    }
}
