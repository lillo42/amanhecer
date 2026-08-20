using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

public interface IProducer
{
    ValueTask ProducerAsync(Message message, IPublication publication, IPipelineContext context);
}