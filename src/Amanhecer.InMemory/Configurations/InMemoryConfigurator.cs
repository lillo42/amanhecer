using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.InMemory.Configurations;

/// <summary>
/// Configures the in-memory gateway: the publications and subscriptions used by the
/// messaging pipeline.
/// </summary>
public class InMemoryConfigurator
{
    private readonly List<InMemoryPublication> _publications = [];
    private readonly List<InMemorySubscription> _subscriptions = [];

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    private Type? _defaultMessageMapperType;

    /// <summary>
    /// Adds publications to the gateway.
    /// </summary>
    /// <param name="configure">A delegate that configures the publications.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemoryConfigurator Publications(Action<InMemoryPublicationsConfigurator> configure)
    {
        var cfg = new InMemoryPublicationsConfigurator();
        configure.Invoke(cfg);

        _publications.AddRange(cfg.ToPublications());
        return this;
    }

    /// <summary>
    /// Adds subscriptions to the gateway.
    /// </summary>
    /// <param name="configure">A delegate that configures the subscriptions.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemoryConfigurator Subscriptions(Action<InMemorySubscriptionsConfigurator> configure)
    {
        var cfg = new InMemorySubscriptionsConfigurator();
        configure.Invoke(cfg);

        _subscriptions.AddRange(cfg.ToSubscriptions());
        return this;
    }

    /// <summary>
    /// Sets the default <see cref="IMessageMapper"/> implementation used by publications and
    /// subscriptions that do not configure one.
    /// </summary>
    /// <param name="messageMapper">The default message mapper implementation type.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="messageMapper"/> does
    /// not implement <see cref="IMessageMapper"/>.</exception>
    public InMemoryConfigurator DefaultMessageMapper(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type messageMapper)
    {
        if (!typeof(IMessageMapper).IsAssignableFrom(messageMapper))
        {
            throw new ArgumentException(
                $"The type '{messageMapper.FullName}' does not implement IMessageMapper.",
                nameof(messageMapper));
        }

        _defaultMessageMapperType = messageMapper;
        return this;
    }

    /// <summary>
    /// Sets the default <see cref="IMessageMapper"/> implementation used by publications and
    /// subscriptions that do not configure one.
    /// </summary>
    /// <typeparam name="TMapper">The default message mapper implementation type.</typeparam>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemoryConfigurator DefaultMessageMapper<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TMapper>() where TMapper : IMessageMapper
    {
        _defaultMessageMapperType = typeof(TMapper);
        return this;
    }

    internal IGateway CreateGateway()
    {
        ApplyDefaultMessageMapper();

        return new InMemoryGateway
        {
            Publications = _publications,
            Subscriptions = _subscriptions
        };
    }

    private void ApplyDefaultMessageMapper()
    {
        if (_defaultMessageMapperType is null)
        {
            return;
        }

        foreach (var publication in _publications)
        {
            publication.MessageMapperType ??= _defaultMessageMapperType;
        }

        foreach (var subscription in _subscriptions)
        {
            subscription.MessageMapperType ??= _defaultMessageMapperType;
        }
    }
}
