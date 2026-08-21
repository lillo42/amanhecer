using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Amanhecer.Configurator;

public class AmanhecerMessagingConfigurator(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;
    public List<IGateway> Gateways { get; } = [];
    public Dictionary<string, IReadOnlyList<AmanhecerTransformerOptions>> TransformerPipeline { get; } = [];
    public List<AmanhecerTransformerOptions> GlobalTransformers { get; } = [];

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

    public AmanhecerMessagingConfigurator AddGateway(IGateway gateway)
    {
        gateway.ProvisionerAsync().GetAwaiter().GetResult();

        Gateways.Add(gateway);
        Services.AddSingleton(gateway);
        return this;
    }
}
