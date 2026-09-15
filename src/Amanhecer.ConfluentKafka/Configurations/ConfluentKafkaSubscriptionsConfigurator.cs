using System;
using System.Collections.Generic;

namespace Amanhecer.ConfluentKafka.Configurations;

/// <summary>
/// Configures the set of Kafka subscriptions a gateway consumes messages through.
/// </summary>
public class ConfluentKafkaSubscriptionsConfigurator
{
    private readonly List<ConfluentKafkaSubscription> _subscriptions = [];

    /// <summary>
    /// Adds a subscription, configured through <see cref="ConfluentKafkaSubscriptionConfigurator"/>.
    /// </summary>
    /// <param name="configure">A delegate that configures the subscription.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaSubscriptionsConfigurator AddSubscription(Action<ConfluentKafkaSubscriptionConfigurator> configure)
    {
        var cfg = new ConfluentKafkaSubscriptionConfigurator();
        configure.Invoke(cfg);

        _subscriptions.Add(cfg.ToSubscription());
        return this;
    }

    /// <summary>
    /// Adds an already-built subscription instance.
    /// </summary>
    /// <param name="subscription">The subscription to add.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaSubscriptionsConfigurator AddSubscription(ConfluentKafkaSubscription subscription)
    {
        _subscriptions.Add(subscription);
        return this;
    }

    internal IEnumerable<ConfluentKafkaSubscription> ToSubscriptions()
    {
        return _subscriptions;
    }
}
