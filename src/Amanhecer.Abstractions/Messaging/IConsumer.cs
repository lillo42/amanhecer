using System;
using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// The runtime actor that consumes messages for a subscription. Started and stopped by the
/// host together with the application lifetime.
/// </summary>
public interface IConsumer
{
    /// <summary>
    /// Starts consuming messages from the transport.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve the pipeline and
    /// the dependencies needed to handle incoming messages.</param>
    /// <param name="cancellationToken">A token that signals the start should be aborted.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the consumer has started.</returns>
    ValueTask StartAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops consuming messages from the transport.
    /// </summary>
    /// <param name="cancellationToken">A token that signals the stop should be aborted.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the consumer has stopped.</returns>
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}