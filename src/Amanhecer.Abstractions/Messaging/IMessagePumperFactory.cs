namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Creates <see cref="IMessagePumper"/> instances, one per running consumer.
/// </summary>
public interface IMessagePumperFactory
{
    /// <summary>
    /// Creates a new <see cref="IMessagePumper"/>.
    /// </summary>
    /// <returns>The created message pumper.</returns>
    IMessagePumper Create();
}
