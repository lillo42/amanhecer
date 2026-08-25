using System;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Base class for declaring a subscription: how messages consumed from a given routing
/// key are handled, including the default CloudEvents attributes expected on them.
/// </summary>
public abstract class Subscription : ISubscription
{
    /// <summary>
    /// Gets or sets the name of the subscription. Defaults to a randomly generated UUID.
    /// </summary>
    public string Name { get; set; } = Uuid.NewGuid().ToString();

    /// <inheritdoc cref="ISubscription.NumberOfConsumer"/>
    public int NumberOfConsumer { get; set; } = 1;

    /// <inheritdoc cref="ISubscription.BufferSize"/>
    public int BufferSize { get; set; } = 1;

    /// <inheritdoc cref="ISubscription.ToRoutingKey"/>
    public required string ToRoutingKey { get; set; }

    /// <inheritdoc cref="ISubscription.DefaultSpecVersion"/>
    public string DefaultSpecVersion { get; set; } = "1.0";

    /// <inheritdoc cref="ISubscription.DefaultSource"/>
    public Uri DefaultSource { get; set; } = new("amanhecer", UriKind.RelativeOrAbsolute);

    /// <inheritdoc cref="ISubscription.DefaultType"/>
    public string DefaultType { get; set; } = "default";

    /// <inheritdoc cref="ISubscription.MessageMapperType"/>
    public Type? MessageMapperType { get; set; }

    /// <inheritdoc cref="ISubscription.Provisioner"/>
    public ISubscriptionProvisoner? Provisioner { get; set; }

    /// <inheritdoc cref="ISubscription.DeadLetterQueueRoutingKey"/>
    public string? DeadLetterQueueRoutingKey { get; set; }

    /// <inheritdoc cref="ISubscription.InvalidMessageRoutingKey"/>
    public string? InvalidMessageRoutingKey { get; set; }

    /// <inheritdoc cref="ISubscription.OnError"/>
    public Func<Message, Exception, IConsumerAction> OnError { get; set; } = static (_, ex) =>
    {
        return ex switch
        {
            // InvalidMessageException => ConsumerActionOnError.MoveToInvalidMessage,
            _ => Defer.Instance
        };
    };
}