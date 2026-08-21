using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// A named sequence of <see cref="IDecodeTransformer"/> components applied to a message
/// being consumed.
/// </summary>
public interface IDecodeTransformerPipeline
{
    /// <summary>
    /// Runs the transformers of the pipeline over a message being consumed.
    /// </summary>
    /// <param name="message">The message to decode.</param>
    /// <param name="context">The context of the pipeline being executed.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipeline has finished.</returns>
    ValueTask DecodeAsync(Message message, IPipelineContext context);
}
