namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Creates <see cref="ITransformerPipeline"/> instances from the transformer pipelines
/// declared in the configuration.
/// </summary>
public interface ITransformerPipelineFactory
{
    /// <summary>
    /// Creates the transformer pipeline registered under the given name.
    /// </summary>
    /// <param name="transformerPipelineName">The name of the transformer pipeline to create.</param>
    /// <param name="context">The context of the pipeline being executed.</param>
    /// <returns>The created transformer pipeline.</returns>
    ITransformerPipeline Create(string transformerPipelineName, IPipelineContext context);
}