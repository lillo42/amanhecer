using System;
using System.Collections.Generic;

namespace Amanhecer.Dekaf.Configurations;

/// <summary>
/// Configures the set of Kafka publications a gateway produces messages through.
/// </summary>
public class DekafPublicationsConfigurator
{
    private readonly List<DekafPublication> _publications = [];

    /// <summary>
    /// Adds a publication, configured through <see cref="DekafPublicationConfigurator"/>.
    /// </summary>
    /// <param name="configure">A delegate that configures the publication.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public DekafPublicationsConfigurator AddPublication(Action<DekafPublicationConfigurator> configure)
    {
        var cfg = new DekafPublicationConfigurator();
        configure.Invoke(cfg);

        _publications.Add(cfg.ToPublication());
        return this;
    }

    /// <summary>
    /// Adds an already-built publication instance.
    /// </summary>
    /// <param name="publication">The publication to add.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public DekafPublicationsConfigurator AddPublication(DekafPublication publication)
    {
        _publications.Add(publication);
        return this;
    }

    internal IEnumerable<DekafPublication> ToPublications()
    {
        return _publications;
    }
}
