using System;
using System.Collections.Generic;

namespace Amanhecer.RabbitMq.Configurations;

/// <summary>
/// Configures the set of RabbitMQ subscriptions a gateway consumes messages through.
/// </summary>
public class RabbitMqSubscriptionsConfigurator
{
    private readonly List<RabbitMqSubscription> _subscriptions = [];

    /// <summary>
    /// Adds a subscription, configured through <see cref="RabbitMqSubscriptionConfigurator"/>.
    /// </summary>
    /// <param name="configure">A delegate that configures the subscription.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqSubscriptionsConfigurator AddSubscription(Action<RabbitMqSubscriptionConfigurator> configure)
    {
        var cfg = new RabbitMqSubscriptionConfigurator();
        configure.Invoke(cfg);

        _subscriptions.Add(cfg.ToSubscription());
        return this;
    }

    /// <summary>
    /// Adds an already-built subscription instance.
    /// </summary>
    /// <param name="subscription">The subscription to add.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqSubscriptionsConfigurator AddSubscription(RabbitMqSubscription subscription)
    {
        _subscriptions.Add(subscription);
        return this;
    }

    internal IEnumerable<RabbitMqSubscription> ToSubscriptions()
    {
        return _subscriptions;
    }
}
