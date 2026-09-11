using System;
using System.Collections.Generic;

namespace Amanhecer.RabbitMq.Configurations;

/// <summary>
/// Configures the set of RabbitMQ publications a gateway produces messages through.
/// </summary>
public class RabbitMqPublicationsConfigurator
{
    private readonly List<RabbitMqPublication> _publications = [];

    /// <summary>
    /// Adds a publication, configured through <see cref="RabbitMqPublicationConfigurator"/>.
    /// </summary>
    /// <param name="configure">A delegate that configures the publication.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqPublicationsConfigurator AddPublication(Action<RabbitMqPublicationConfigurator> configure)
    {
        var cfg = new RabbitMqPublicationConfigurator();
        configure.Invoke(cfg);

        _publications.Add(cfg.ToPublication());
        return this;
    }

    /// <summary>
    /// Adds an already-built publication instance.
    /// </summary>
    /// <param name="publication">The publication to add.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the publication has no exchange configured.
    /// </exception>
    public RabbitMqPublicationsConfigurator AddPublication(RabbitMqPublication publication)
    {
        if (publication.Exchange is null)
        {
            throw new InvalidOperationException(
                $"The publication '{publication.RoutingKey}' has no exchange configured.");
        }

        _publications.Add(publication);
        return this;
    }

    internal IEnumerable<RabbitMqPublication> ToPublications()
    {
        return _publications;
    }
}
