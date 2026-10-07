using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using RabbitMQ.Stream.Client;

namespace Amanhecer.RabbitMq.Streams.Provisioners;

/// <summary>
/// A provisioner that creates the stream of a publication or subscription if it does not
/// exist yet. A stream that already exists is left untouched.
/// </summary>
public class CreateStream : IPublicationProvisioner, ISubscriptionProvisioner
{
    /// <summary>Gets or sets the maximum age of messages in the stream before they are deleted.</summary>
    public TimeSpan? MaxAge { get; set; }

    /// <summary>Gets or sets the maximum total size in bytes of the stream before old segments are deleted.</summary>
    public ulong? MaxLengthBytes { get; set; }

    /// <summary>Gets or sets the maximum size in bytes of each stream segment file.</summary>
    public int? MaxSegmentSizeBytes { get; set; }

    /// <summary>Gets or sets the leader-locator strategy used when creating the stream.</summary>
    public LeaderLocator? LeaderLocator { get; set; }

    /// <inheritdoc/>
    public Task ExecuteAsync(IGateway gateway, IPublication publication)
    {
        if (publication is not RabbitMqStreamPublication streamPublication)
        {
            throw new ArgumentException(
                $"The publication must be a {nameof(RabbitMqStreamPublication)}.",
                nameof(publication));
        }

        return CreateAsync(gateway, streamPublication.Stream);
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

        return CreateAsync(gateway, streamSubscription.Stream);
    }

    private async Task CreateAsync(IGateway gateway, string streamName)
    {
        if (gateway is not RabbitMqStreamGateway streamGateway)
        {
            throw new ArgumentException(
                $"The gateway must be a {nameof(RabbitMqStreamGateway)}.",
                nameof(gateway));
        }

        var system = await streamGateway.GetOrCreateStreamSystem();

        var spec = new StreamSpec(streamName);
        if (MaxAge.HasValue)
        {
            spec.MaxAge = MaxAge.Value;
        }

        if (MaxLengthBytes.HasValue)
        {
            spec.MaxLengthBytes = MaxLengthBytes.Value;
        }

        if (MaxSegmentSizeBytes.HasValue)
        {
            spec.MaxSegmentSizeBytes = MaxSegmentSizeBytes.Value;
        }

        if (LeaderLocator.HasValue)
        {
            spec.LeaderLocator = LeaderLocator.Value;
        }

        // StreamSystem.CreateStream returns normally when the stream already exists;
        // CreateStreamException is only thrown for other errors.
        await system.CreateStream(spec);
    }
}