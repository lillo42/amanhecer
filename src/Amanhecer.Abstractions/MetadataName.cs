namespace Amanhecer.Abstractions;

/// <summary>
/// Well-known names of the entries stored in the metadata dictionary of a context.
/// </summary>
public static class MetadataName
{
    /// <summary>
    /// The name of the metadata entry holding the messaging gateway the message flows through.
    /// </summary>
    public const string MessagingGateway = "Amanhecer.Messaging.Gateway";
    
    /// <summary>
    /// The name of the metadata entry holding the <see cref="Messaging.IPublication"/> the
    /// message is published for.
    /// </summary>
    public const string Publication = "Amanhecer.Messaging.Publication";

    /// <summary>
    /// The name of the metadata entry holding the routing key the message is published to.
    /// </summary>
    public const string PublicationRoutingKey = "Amanhecer.Messaging.Publication.RoutingKey";

    /// <summary>
    /// The name of the metadata entry holding the <see cref="Messaging.IMessageMapper"/> used
    /// to map the published message.
    /// </summary>
    public const string PublicationMessageMapper = "Amanhecer.Messaging.Publication.MessageMapper";
    
    /// <summary>
    /// The name of the metadata entry holding the <see cref="Messaging.ISubscription"/> the
    /// message was consumed from.
    /// </summary>
    public const string Subscription = "Amanhecer.Messaging.Subscription";

    /// <summary>
    /// The name of the metadata entry holding the <see cref="Messaging.IMessageMapper"/> used
    /// to map the consumed message.
    /// </summary>
    public const string SubscriptionMessageMapper = "Amanhecer.Messaging.Subscription";

    /// <summary>
    /// The name of the metadata entry holding the request <see cref="System.Type"/> the pipeline
    /// handling the consumed message expects.
    /// </summary>
    public const string RequestType = "Amanhecer.Messaging.RequestType";
    
    public const string OriginalRequest= "Amanhecer.Messaging.Request";

    public const string MessageMapperType = "Amanhecer.Messaging.Mapper";
    
    public const string OriginalMessage = "Amanhecer.Messaging.Message";
}
