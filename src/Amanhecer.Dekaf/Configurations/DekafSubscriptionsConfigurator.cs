using System;
using System.Collections.Generic;

namespace Amanhecer.Dekaf.Configurations;

/// <summary>
/// Configures the set of Kafka subscriptions a gateway consumes messages through.
/// </summary>
public class DekafSubscriptionsConfigurator
{
    private readonly List<DekafSubscription> _subscriptions = [];

    /// <summary>
    /// Adds a subscription, configured through <see cref="DekafSubscriptionConfigurator"/>.
    /// </summary>
    /// <param name="configure">A delegate that configures the subscription.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public DekafSubscriptionsConfigurator AddSubscription(Action<DekafSubscriptionConfigurator> configure)
    {
        var cfg = new DekafSubscriptionConfigurator();
        configure.Invoke(cfg);

        _subscriptions.Add(cfg.ToSubscription());
        return this;
    }

    /// <summary>
    /// Adds an already-built subscription instance.
    /// </summary>
    /// <param name="subscription">The subscription to add.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public DekafSubscriptionsConfigurator AddSubscription(DekafSubscription subscription)
    {
        _subscriptions.Add(subscription);
        return this;
    }

    internal IEnumerable<DekafSubscription> ToSubscriptions()
    {
        return _subscriptions;
    }
}
