using System;
using System.Collections.Generic;

namespace Amanhecer.InMemory.Configurations;

/// <summary>
/// Configures the set of in-memory subscriptions a gateway consumes messages through.
/// </summary>
public class InMemorySubscriptionsConfigurator
{
    private readonly List<InMemorySubscription> _subscriptions = [];

    /// <summary>
    /// Adds a subscription configured through <see cref="InMemorySubscriptionConfigurator"/>.
    /// </summary>
    /// <param name="configure">A delegate that configures the subscription.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemorySubscriptionsConfigurator AddSubscription(Action<InMemorySubscriptionConfigurator> configure)
    {
        var cfg = new InMemorySubscriptionConfigurator();
        configure.Invoke(cfg);

        _subscriptions.Add(cfg.ToSubscription());
        return this;
    }

    /// <summary>
    /// Adds an already-built subscription instance.
    /// </summary>
    /// <param name="subscription">The subscription to add.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemorySubscriptionsConfigurator AddSubscription(InMemorySubscription subscription)
    {
        _subscriptions.Add(subscription);
        return this;
    }

    internal IEnumerable<InMemorySubscription> ToSubscriptions()
    {
        return _subscriptions;
    }
}
