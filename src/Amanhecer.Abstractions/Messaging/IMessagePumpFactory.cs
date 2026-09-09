namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Creates <see cref="IMessagePump"/> instances, one per running consumer.
/// </summary>
public interface IMessagePumpFactory
{
    /// <summary>
    /// Creates a new <see cref="IMessagePump"/>.
    /// </summary>
    /// <returns>The created message pump.</returns>
    IMessagePump Create();
}
