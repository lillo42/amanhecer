using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// A named sequence of <see cref="IEncodeTransformer"/> components applied to a message
/// being published.
/// </summary>
public interface IEncodeTransformerPipeline
{
    /// <summary>
    /// Runs the transformers of the pipeline over a message being published.
    /// </summary>
    /// <param name="message">The message to encode.</param>
    /// <param name="context">The context of the pipeline being executed.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipeline has finished.</returns>
    ValueTask EncodeAsync(Message message, IPipelineContext context);
}
