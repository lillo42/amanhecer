using System;
using System.Collections.Generic;

namespace Amanhecer.RabbitMq.Streams.Configurations;

/// <summary>
/// Configures the set of RabbitMQ Streams publications a gateway produces messages through.
/// </summary>
public class RabbitMqStreamPublicationsConfigurator
{
    private readonly List<RabbitMqStreamPublication> _publications = [];

    /// <summary>
    /// Adds a publication, configured through <see cref="RabbitMqStreamPublicationConfigurator"/>.
    /// </summary>
    /// <param name="configure">A delegate that configures the publication.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamPublicationsConfigurator AddPublication(Action<RabbitMqStreamPublicationConfigurator> configure)
    {
        var cfg = new RabbitMqStreamPublicationConfigurator();
        configure.Invoke(cfg);

        _publications.Add(cfg.ToPublication());
        return this;
    }

    /// <summary>
    /// Adds an already-built publication instance.
    /// </summary>
    /// <param name="publication">The publication to add.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public RabbitMqStreamPublicationsConfigurator AddPublication(RabbitMqStreamPublication publication)
    {
        _publications.Add(publication ?? throw new ArgumentNullException(nameof(publication)));
        return this;
    }

    internal IEnumerable<RabbitMqStreamPublication> ToPublications()
    {
        return _publications;
    }
}
