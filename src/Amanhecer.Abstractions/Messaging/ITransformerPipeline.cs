using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

public interface ITransformerPipeline
{
    ValueTask DecodeAsync(Message message, IPipelineContext context);
}