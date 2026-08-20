namespace Amanhecer.Abstractions.Messaging;

public interface ITransformerPipelineFactory
{
    ITransformerPipeline Create(IPipelineContext context);
}