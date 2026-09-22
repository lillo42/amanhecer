using System.Collections.Generic;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// A logical grouping of the publications and subscriptions exposed over a single
/// broker connection, binding the messaging abstractions to a concrete transport.
/// </summary>
public interface IGateway
{
    /// <summary>
    /// Gets the name of the gateway.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the publications declared on this gateway.
    /// </summary>
    IEnumerable<IPublication> Publications { get; set; }

    /// <summary>
    /// Gets the subscriptions declared on this gateway.
    /// </summary>
    IEnumerable<ISubscription> Subscriptions { get; set; }

    /// <summary>
    /// Executes the provisioner of every publication and subscription that declares one,
    /// so the transport resources (exchanges, queues, bindings, ...) exist before the
    /// gateway is used.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that completes when all provisioners have run.</returns>
    ValueTask ProvisionerAsync();

    /// <summary>
    /// Creates the producers able to publish messages through this gateway.
    /// </summary>
    /// <returns>A dictionary of producers, keyed by the routing key of the publication
    /// they publish through.</returns>
    IReadOnlyDictionary<string, IProducer> CreateProducers();

    /// <summary>
    /// Creates the consumers that receive messages for this gateway's subscriptions.
    /// </summary>
    /// <returns>The consumers to start.</returns>
    IConsumer CreateConsumer(ISubscription subscription);
}