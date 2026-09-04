using System.Linq;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Configurator;

namespace Amanhecer.Messaging;

/// <summary>
/// An <see cref="IEncodeTransformerPipelineFactory"/> that builds pipelines from the transformer
/// pipelines declared in <see cref="AmanhecerTransformerPipelineOptions"/>.
/// </summary>
/// <param name="options">The configured transformer pipelines.</param>
/// <param name="transformerFactory">The factory used to create the configured transformers.</param>
public class AmanhecerEncodeTransformerPipelineFactory(
    AmanhecerTransformerPipelineOptions options,
    IEncodeTransformerFactory transformerFactory) : IEncodeTransformerPipelineFactory
{
    /// <inheritdoc />
    public IEncodeTransformerPipeline Create(string transformerPipelineName, AmanhecerContext context)
    {
        if (!options.Configuration.TryGetValue(transformerPipelineName, out var transformers))
        {
            return new AmanhecerEncodeTransformerPipeline([]);
        }

        return new AmanhecerEncodeTransformerPipeline([
            .. transformers
                .Where(x => typeof(IEncodeTransformer).IsAssignableFrom(x.TransformerType))
                .Select(x => transformerFactory.Create(x.TransformerType, x.Metadata, context))
        ]);
    }
}
