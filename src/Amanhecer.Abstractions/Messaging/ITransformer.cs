using System;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// A component of an encode transformer pipeline, transforming a message on its way out.
/// </summary>
public interface IEncodeTransformer
{
    /// <summary>
    /// Initialises the transformer with the metadata supplied when the pipeline was built.
    /// </summary>
    /// <param name="metadata">Optional metadata used to configure the transformer instance.</param>
    void Initialize(object? metadata);

    /// <summary>
    /// Transforms a message on its way out. Invoke <paramref name="next"/> to continue
    /// executing the encoding pipeline.
    /// </summary>
    /// <param name="message">The message being encoded.</param>
    /// <param name="context">The context of the pipeline being executed.</param>
    /// <param name="next">A delegate that invokes the next transformer in the pipeline.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the transformer has finished.</returns>
    ValueTask EncodeAsync(Message message,
        IPipelineContext context,
        Func<Message, IPipelineContext, ValueTask> next);
}

/// <summary>
/// A component of a decode transformer pipeline, transforming a message on its way in.
/// </summary>
public interface IDecodeTransformer
{
    /// <summary>
    /// Initialises the transformer with the metadata supplied when the pipeline was built.
    /// </summary>
    /// <param name="metadata">Optional metadata used to configure the transformer instance.</param>
    void Initialize(object? metadata);

    /// <summary>
    /// Transforms a message on its way in. Invoke <paramref name="next"/> to continue
    /// executing the decoding pipeline.
    /// </summary>
    /// <param name="message">The message being decoded.</param>
    /// <param name="context">The context of the pipeline being executed.</param>
    /// <param name="next">A delegate that invokes the next transformer in the pipeline.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the transformer has finished.</returns>
    ValueTask DecodeAsync(Message message,
        IPipelineContext context,
        Func<Message, IPipelineContext, ValueTask> next);
}

/// <summary>
/// A transformer that applies in both directions. Implement this for symmetric transforms
/// (compression, encryption, ...) so the pair is registered and ordered together.
/// </summary>
public interface ITransformer : IEncodeTransformer, IDecodeTransformer;
