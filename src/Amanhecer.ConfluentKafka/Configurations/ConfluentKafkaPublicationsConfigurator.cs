using System;
using System.Collections.Generic;

namespace Amanhecer.ConfluentKafka.Configurations;

/// <summary>
/// Configures the set of Kafka publications a gateway produces messages through.
/// </summary>
public class ConfluentKafkaPublicationsConfigurator
{
    private readonly List<ConfluentKafkaPublication> _publications = [];

    /// <summary>
    /// Adds a publication, configured through <see cref="ConfluentKafkaPublicationConfigurator"/>.
    /// </summary>
    /// <param name="configure">A delegate that configures the publication.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaPublicationsConfigurator AddPublication(Action<ConfluentKafkaPublicationConfigurator> configure)
    {
        var cfg = new ConfluentKafkaPublicationConfigurator();
        configure.Invoke(cfg);

        _publications.Add(cfg.ToPublication());
        return this;
    }

    /// <summary>
    /// Adds an already-built publication instance.
    /// </summary>
    /// <param name="publication">The publication to add.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public ConfluentKafkaPublicationsConfigurator AddPublication(ConfluentKafkaPublication publication)
    {
        _publications.Add(publication);
        return this;
    }

    internal IEnumerable<ConfluentKafkaPublication> ToPublications()
    {
        return _publications;
    }
}
