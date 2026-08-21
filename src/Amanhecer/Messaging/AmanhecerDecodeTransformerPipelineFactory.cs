using System.Linq;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Configurator;

namespace Amanhecer.Messaging;

public class AmanhecerDecodeTransformerPipelineFactory(
    AmanhecerTransformerPipelineOptions options,
    IDecodeTransformerFactory transformerFactory) : IDecodeTransformerPipelineFactory
{
    public IDecodeTransformerPipeline Create(string transformerPipelineName, IPipelineContext context)
    {
        if (!options.Configuration.TryGetValue(transformerPipelineName, out var transformers))
        {
            return new AmanhecerDecodeTransformerPipeline([]);
        }

        return new AmanhecerDecodeTransformerPipeline([
            .. transformers
                .Where(x => typeof(IDecodeTransformer).IsAssignableFrom(x.TransformerType))
                .Select(x => transformerFactory.Create(x.TransformerType, x.Metadata))
        ]);
    }
}
