using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Base class for a gateway: a logical grouping of the publications and subscriptions a
/// broker connection exposes. Implementations bind the abstractions to a concrete transport.
/// </summary>
/// <typeparam name="TPublication">The type used to declare publications on this gateway.</typeparam>
/// <typeparam name="TSubscription">The type used to declare subscriptions on this gateway.</typeparam>
public abstract class Gateway<TPublication, TSubscription> : IGateway
    where TPublication : IPublication
    where TSubscription : ISubscription
{
    /// <summary>
    /// Gets or sets the name of the gateway. Defaults to a randomly generated UUID.
    /// </summary>
    public string Name { get; set; } = Uuid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the publications declared on this gateway.
    /// </summary>
    public List<TPublication> Publications { get; set; } = [];

    /// <summary>
    /// Gets or sets the subscriptions declared on this gateway.
    /// </summary>
    public List<TSubscription> Subscriptions { get; set; } = [];


    IEnumerable<IPublication> IGateway.Publications
    {
        get => Publications.Cast<IPublication>();
        set => Publications = value.Cast<TPublication>().ToList();
    }

    IEnumerable<ISubscription> IGateway.Subscriptions
    {
        get => Subscriptions.Cast<ISubscription>();
        set => Subscriptions = value.Cast<TSubscription>().ToList();
    }

    /// <summary>
    /// Executes the provisioner of every publication and subscription that declares one,
    /// so the transport resources (exchanges, queues, bindings, ...) exist before the
    /// gateway is used.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that completes when all provisioners have run.</returns>
    public virtual async ValueTask ProvisionerAsync()
    {
        foreach (var publication in Publications)
        {
            if (publication.Provisioner != null)
            {
                await publication
                    .Provisioner
                    .ExecuteAsync(this, publication);
            }
        }

        foreach (var subscription in Subscriptions)
        {
            if (subscription.Provisioner != null)
            {
                await subscription
                    .Provisioner
                    .ExecuteAsync(this, subscription);
            }
        }
    }

    /// <summary>
    /// Creates the producers able to publish messages through this gateway.
    /// </summary>
    /// <returns>A dictionary of producers, keyed by the routing key of the publication
    /// they publish through.</returns>
    public abstract IReadOnlyDictionary<string, IProducer> CreateProducers();

    /// <summary>
    /// Creates the consumers that receive messages for this gateway's subscriptions.
    /// </summary>
    /// <returns>The consumers to start.</returns>
    public abstract IConsumer CreateConsumer(ISubscription subscription);
}