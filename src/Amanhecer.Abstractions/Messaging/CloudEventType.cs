namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// The CloudEvents content mode used to encode messages: how the event attributes and data
/// are laid out on the wire.
/// </summary>
public enum CloudEventType
{
    /// <summary>
    /// Binary content mode: event attributes are carried as message headers and the message
    /// payload contains only the event data.
    /// </summary>
    Binary,

    /// <summary>
    /// Structured content mode: the whole event, attributes and data, is serialized as JSON
    /// in the message payload.
    /// </summary>
    Json
}
