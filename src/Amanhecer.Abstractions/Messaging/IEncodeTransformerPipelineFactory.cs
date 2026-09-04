namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Creates <see cref="IEncodeTransformerPipeline"/> instances from the transformer pipelines
/// declared in the configuration.
/// </summary>
public interface IEncodeTransformerPipelineFactory
{
    /// <summary>
    /// Creates the encode transformer pipeline registered under the given name.
    /// </summary>
    /// <param name="transformerPipelineName">The name of the transformer pipeline to create.</param>
    /// <param name="context">The context of the pipeline being executed.</param>
    /// <returns>The created encode transformer pipeline.</returns>
    IEncodeTransformerPipeline Create(string transformerPipelineName, AmanhecerContext context);
}
