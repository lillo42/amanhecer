using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.InMemory;

/// <summary>
/// An <see cref="IProducer"/> that publishes messages into an in-memory queue.
/// </summary>
public class InMemoryProducer(QueueManagement queues) : IProducer
{
    /// <inheritdoc/>
    public async ValueTask ProduceAsync(Message message, IPublication publication, AmanhecerContext context)
    {
        if (publication is not InMemoryPublication inMemoryPublication)
        {
            throw new ArgumentException(
                $"The publication must be a {nameof(InMemoryPublication)}.",
                nameof(publication));
        }

        var channel = queues.GetChannels(inMemoryPublication.QueueName);
        await channel.Writer.WriteAsync(message)
            .ConfigureAwait(context.ContinueOnCapturedContext);
    }
}
