using System.Linq;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Configurator;

namespace Amanhecer.Messaging;

public class AmanhecerEncodeTransformerPipelineFactory(
    AmanhecerTransformerPipelineOptions options,
    IEncodeTransformerFactory transformerFactory) : IEncodeTransformerPipelineFactory
{
    public IEncodeTransformerPipeline Create(string transformerPipelineName, IPipelineContext context)
    {
        if (!options.Configuration.TryGetValue(transformerPipelineName, out var transformers))
        {
            return new AmanhecerEncodeTransformerPipeline([]);
        }

        return new AmanhecerEncodeTransformerPipeline([
            .. transformers
                .Where(x => typeof(IEncodeTransformer).IsAssignableFrom(x.TransformerType))
                .Select(x => transformerFactory.Create(x.TransformerType, x.Metadata))
        ]);
    }
}
