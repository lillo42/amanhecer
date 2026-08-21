namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Creates <see cref="IDecodeTransformerPipeline"/> instances from the transformer pipelines
/// declared in the configuration.
/// </summary>
public interface IDecodeTransformerPipelineFactory
{
    /// <summary>
    /// Creates the decode transformer pipeline registered under the given name.
    /// </summary>
    /// <param name="transformerPipelineName">The name of the transformer pipeline to create.</param>
    /// <param name="context">The context of the pipeline being executed.</param>
    /// <returns>The created decode transformer pipeline.</returns>
    IDecodeTransformerPipeline Create(string transformerPipelineName, IPipelineContext context);
}
