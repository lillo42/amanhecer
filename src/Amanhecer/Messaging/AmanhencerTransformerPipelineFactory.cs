using System.Linq;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Messaging;

public class AmanhencerTransformerPipelineFactory(
    AmanhecerTransformerPipelineOptions options,
    ITransformerFactory transformerFactory) : ITransformerPipelineFactory
{
    public ITransformerPipeline Create(IPipelineContext context)
    {
        if (options.Configuration.TryGetValue(context.RoutingKey, out var transformers))
        {
            return new AmanhecerTransformerPipeline([
                .. transformers
                    .Select(x => transformerFactory.Create(x.MiddlewareType, x.Metadata))
            ]);
        }

        return new AmanhecerTransformerPipeline([]);
    }
}