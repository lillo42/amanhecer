using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging.Transformers;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.Messaging;

/// <summary>
/// An <see cref="IEncodeTransformerFactory"/> that resolves encode transformers from the
/// application's service provider.
/// </summary>
/// <param name="provider">The service provider used to resolve transformer instances.</param>
public class AmanhecerEncodeTransformerFactory(IServiceProvider provider) : IEncodeTransformerFactory
{
    /// <inheritdoc />
    public IEncodeTransformer Create(Type transformerType, object? metadata, AmanhecerContext context)
    {
        if (transformerType == typeof(AnonymousEncodeTransformer))
        {
            var func = (Func<Message, AmanhecerContext, Func<Message, AmanhecerContext, ValueTask>, ValueTask>)metadata!;
            return new AnonymousEncodeTransformer(func);
        }
        
        if (metadata != null)
        {
            context.SetMetadata(metadata);
        }

        var transformer = (IEncodeTransformer)provider.GetRequiredService(transformerType);
        return transformer;
    }
}