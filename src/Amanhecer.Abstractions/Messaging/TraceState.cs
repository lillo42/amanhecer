using System.Collections.Generic;
using System.Linq;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// A set of string key/value pairs propagated alongside a message, following the W3C
/// Trace Context <c>tracestate</c> header.
/// </summary>
public class TraceState : Dictionary<string, string>
{
    /// <summary>
    /// Initializes a new, empty <see cref="TraceState"/> instance.
    /// </summary>
    public TraceState()
    {
    }

    /// <summary>
    /// Initializes a new <see cref="TraceState"/> instance from an existing dictionary.
    /// </summary>
    /// <param name="items">The key/value pairs to copy into the trace state.</param>
    public TraceState(IDictionary<string, string> items) : base(items)
    {
    }

    /// <summary>
    /// Parses a W3C Trace Context <c>tracestate</c> header value into a
    /// <see cref="TraceState"/> instance. Entries that are not in the <c>key=value</c>
    /// form are ignored.
    /// </summary>
    /// <param name="baggage">The serialized trace state, as a comma-separated list of
    /// <c>key=value</c> pairs.</param>
    /// <returns>The parsed <see cref="TraceState"/>.</returns>
    public static TraceState FromString(string baggage)
    {
        var dict = baggage
            .Split(',')
            .Select(x => x.Split('='))
            .Where(x => x.Length == 2)
            .ToDictionary(x => x[0], x => x[1]);

        return new TraceState(dict);
    }

    /// <summary>
    /// Serializes the trace state into a W3C Trace Context <c>tracestate</c> header value:
    /// a comma-separated list of <c>key=value</c> pairs.
    /// </summary>
    /// <returns>The serialized trace state.</returns>
    public override string ToString()
    {
        return string.Join(",", this.Select(x => $"{x.Key}={x.Value}"));
    }
}