namespace Amanhecer.Abstractions.Messaging;

public interface ITransformerPipelineFactory
{
    ITransformerPipeline Create(string transformerPipelineName, IPipelineContext context);
}