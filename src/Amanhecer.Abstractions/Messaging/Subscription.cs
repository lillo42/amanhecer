using System;
using System.Net.Mime;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Base class for declaring a subscription: how messages consumed from a given routing
/// key are handled, including the default CloudEvents attributes expected on them.
/// </summary>
public abstract class Subscription(string toRoutingKey) : ISubscription
{
    /// <inheritdoc cref="ISubscription.Name"/>
    public string Name { get; set; } = Uuid.NewGuid().ToString();

    /// <inheritdoc cref="ISubscription.NumberOfConsumer"/>
    public int NumberOfConsumer { get; set; } = 1;

    /// <inheritdoc cref="ISubscription.BufferSize"/>
    public int BufferSize { get; set; } = 1;

    /// <inheritdoc cref="ISubscription.NoMessageDelay" />
    public TimeSpan NoMessageDelay { get; set; } = TimeSpan.FromMilliseconds(300); 

    /// <inheritdoc cref="ISubscription.FailureDelay" />
    public TimeSpan FailureDelay { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <inheritdoc cref="ISubscription.ReceiveMessageTimeout" />
    public TimeSpan ReceiveMessageTimeout { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <inheritdoc cref="ISubscription.ToRoutingKey" />
    public string ToRoutingKey { get; set; } = toRoutingKey;

    /// <inheritdoc cref="ISubscription.DefaultContentType" />
    public ContentType DefaultContentType { get; set; } = new("text/plain");
    
    /// <inheritdoc cref="ISubscription.DefaultDataSchema" />
    public Uri? DefaultDataSchema { get; set; }
    
    /// <inheritdoc cref="ISubscription.DefaultReplyTo" />
    public string? DefaultReplyTo { get; set; }

    /// <inheritdoc cref="ISubscription.DefaultSpecVersion" />
    public string DefaultSpecVersion { get; set; } = "1.0";

    /// <inheritdoc cref="ISubscription.DefaultSubject" />
    public string? DefaultSubject { get; set; }

    /// <inheritdoc cref="ISubscription.DefaultSource" />
    public Uri DefaultSource { get; set; } = new("amanhecer", UriKind.RelativeOrAbsolute);

    /// <inheritdoc cref="ISubscription.DefaultType" />
    public string DefaultType { get; set; } = "default";

    /// <inheritdoc cref="ISubscription.MessageMapperType" />
    public Type? MessageMapperType { get; set; }

    /// <inheritdoc cref="ISubscription.Provisioner" />
    public ISubscriptionProvisoner? Provisioner { get; set; }

    /// <inheritdoc cref="ISubscription.DeadLetterQueueRoutingKey" />
    public string? DeadLetterQueueRoutingKey { get; set; }

    /// <inheritdoc cref="ISubscription.InvalidMessageRoutingKey" />
    public string? InvalidMessageRoutingKey { get; set; }

    /// <inheritdoc cref="ISubscription.ContinueOnCapturedContext" />
    public bool ContinueOnCapturedContext { get; set; } = false;

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