using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.RabbitMq.Streams.Provisioners;

public class ValidateSuperStreamExists : IPublicationProvisioner, ISubscriptionProvisioner
{
    /// <inheritdoc/>
    public Task ExecuteAsync(IGateway gateway, IPublication publication)
    {
        if (publication is not RabbitMqStreamPublication streamPublication)
        {
            throw new ArgumentException(
                $"The publication must be a {nameof(RabbitMqStreamPublication)}.",
                nameof(publication));
        }

        return ValidateAsync(gateway, streamPublication.Stream);
    }

    /// <inheritdoc/>
    public Task ExecuteAsync(IGateway gateway, ISubscription subscription)
    {
        if (subscription is not RabbitMqStreamSubscription streamSubscription)
        {
            throw new ArgumentException(
                $"The subscription must be a {nameof(RabbitMqStreamSubscription)}.",
                nameof(subscription));
        }

        return ValidateAsync(gateway, streamSubscription.Stream);
    }

    private static async Task ValidateAsync(IGateway gateway, string streamName)
    {
        if (gateway is not RabbitMqStreamGateway streamGateway)
        {
            throw new ArgumentException(
                $"The gateway must be a {nameof(RabbitMqStreamGateway)}.",
                nameof(gateway));
        }

        var system = await streamGateway.GetOrCreateStreamSystem();

        var exists = await system.SuperStreamExists(streamName);
        if (!exists)
        {
            throw new InvalidOperationException(
                $"Stream '{streamName}' does not exist on the broker.");
        }
    }
}