using System;
using System.Collections.Generic;

namespace Amanhecer.InMemory.Configurations;

/// <summary>
/// Configures the set of in-memory publications a gateway produces messages through.
/// </summary>
public class InMemoryPublicationsConfigurator
{
    private readonly List<InMemoryPublication> _publications = [];

    /// <summary>
    /// Adds a publication configured through <see cref="InMemoryPublicationConfigurator"/>.
    /// </summary>
    /// <param name="configure">A delegate that configures the publication.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemoryPublicationsConfigurator AddPublication(Action<InMemoryPublicationConfigurator> configure)
    {
        var cfg = new InMemoryPublicationConfigurator();
        configure.Invoke(cfg);

        _publications.Add(cfg.ToPublication());
        return this;
    }

    /// <summary>
    /// Adds an already-built publication instance.
    /// </summary>
    /// <param name="publication">The publication to add.</param>
    /// <returns>The configurator instance for method chaining.</returns>
    public InMemoryPublicationsConfigurator AddPublication(InMemoryPublication publication)
    {
        _publications.Add(publication);
        return this;
    }

    internal IEnumerable<InMemoryPublication> ToPublications()
    {
        return _publications;
    }
}
