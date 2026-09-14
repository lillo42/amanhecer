using System.Collections.Generic;
using System.Threading.Channels;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.InMemory;

/// <summary>
/// Stores the in-memory channels keyed by queue name.
/// </summary>
public class QueueManagement
{
    private readonly Dictionary<string, Channel<Message>> _channels = [];

    /// <summary>
    /// Returns whether a channel has already been registered for the queue.
    /// </summary>
    /// <param name="queueName">The queue name.</param>
    /// <returns><c>true</c> when a channel exists for the queue; otherwise, <c>false</c>.</returns>
    public bool Exists(string queueName)
    {
        return _channels.ContainsKey(queueName);
    }

    /// <summary>
    /// Adds a queue channel or replaces the existing channel for the same queue name.
    /// </summary>
    /// <param name="queueName">The queue name.</param>
    /// <param name="channel">The queue channel.</param>
    public void AddChannel(string queueName, Channel<Message> channel)
    {
        _channels[queueName] = channel;
    }

    /// <summary>
    /// Gets the channel registered for the queue.
    /// </summary>
    /// <param name="queueName">The queue name.</param>
    /// <returns>The channel registered for the queue.</returns>
    public Channel<Message> GetChannels(string queueName)
    {
        return _channels[queueName];
    }
}
