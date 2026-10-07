using System;
using System.Collections.Generic;

namespace Amanhecer.RabbitMq.Streams.Configurations;

/// <summary>
/// Configures the set of RabbitMQ Streams subscriptions a gateway consumes messages through.
/// </summary>
public class RabbitMqStreamSubscriptionsConfigurator
{
    private readonly List<RabbitMqStreamSubscription> _subscriptions = [];

    /// <summary>
    /// Adds a subscription, configured through <see cref="RabbitMqStreamSubscriptionConfigurator"/>.
    /// </summary>
    /// <param name="configure">A delegate that configures the subscription.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionsConfigurator AddSubscription(Action<RabbitMqStreamSubscriptionConfigurator> configure)
    {
        var cfg = new RabbitMqStreamSubscriptionConfigurator();
        configure.Invoke(cfg);

        _subscriptions.Add(cfg.ToSubscription());
        return this;
    }

    /// <summary>
    /// Adds an already-built subscription instance.
    /// </summary>
    /// <param name="subscription">The subscription to add.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamSubscriptionsConfigurator AddSubscription(RabbitMqStreamSubscription subscription)
    {
        _subscriptions.Add(subscription ?? throw new ArgumentNullException(nameof(subscription)));
        return this;
    }

    internal IEnumerable<RabbitMqStreamSubscription> ToSubscriptions()
    {
        return _subscriptions;
    }
}
