namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Marker for application types that carry the identifier of the message they map to.
/// </summary>
public interface IMessageId
{
    /// <summary>
    /// Gets or sets the message identifier.
    /// </summary>
    string Id { get; set; }
}