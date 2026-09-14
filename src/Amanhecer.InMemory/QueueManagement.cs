using System;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.InMemory;

/// <summary>
/// Stores the in-memory channels keyed by queue name.
/// </summary>
/// <remarks>
/// Provisioners write to the registry while producers on other threads read from it, so the
/// backing store is concurrent.
/// </remarks>
public class QueueManagement
{
    private readonly ConcurrentDictionary<string, Channel<Message>> _channels = new();

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
    /// <exception cref="InvalidOperationException">
    /// No channel has been registered for the queue.
    /// </exception>
    public Channel<Message> GetChannels(string queueName)
    {
        if (_channels.TryGetValue(queueName, out var channel))
        {
            return channel;
        }

        throw new InvalidOperationException(
            $"Queue '{queueName}' has not been provisioned. Configure a provisioner that creates it " +
            "with CreateOrOverride, or add the channel to the gateway queue registry before use.");
    }
}
