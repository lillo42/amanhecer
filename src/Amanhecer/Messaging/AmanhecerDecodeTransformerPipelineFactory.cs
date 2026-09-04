using System.Linq;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Configurator;

namespace Amanhecer.Messaging;

/// <summary>
/// An <see cref="IDecodeTransformerPipelineFactory"/> that builds pipelines from the transformer
/// pipelines declared in <see cref="AmanhecerTransformerPipelineOptions"/>.
/// </summary>
/// <param name="options">The configured transformer pipelines.</param>
/// <param name="transformerFactory">The factory used to create the configured transformers.</param>
public class AmanhecerDecodeTransformerPipelineFactory(
    AmanhecerTransformerPipelineOptions options,
    IDecodeTransformerFactory transformerFactory) : IDecodeTransformerPipelineFactory
{
    /// <inheritdoc />
    public IDecodeTransformerPipeline Create(string transformerPipelineName, AmanhecerContext context)
    {
        if (!options.Configuration.TryGetValue(transformerPipelineName, out var transformers))
        {
            return new AmanhecerDecodeTransformerPipeline([]);
        }

        return new AmanhecerDecodeTransformerPipeline([
            .. transformers
                .Where(x => typeof(IDecodeTransformer).IsAssignableFrom(x.TransformerType))
                .Select(x => transformerFactory.Create(x.TransformerType, x.Metadata, context))
        ]);
    }
}
