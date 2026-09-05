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
    /// <see cref="TraceState"/> instance. Entries are split on the first <c>=</c> (values
    /// may contain <c>=</c>), entries that are not in the <c>key=value</c> form are ignored,
    /// and when a key appears more than once the last value wins.
    /// </summary>
    /// <param name="traceState">The serialized trace state, as a comma-separated list of
    /// <c>key=value</c> pairs.</param>
    /// <returns>The parsed <see cref="TraceState"/>.</returns>
    public static TraceState FromString(string traceState)
    {
        var result = new TraceState();
        foreach (var entry in traceState.Split(','))
        {
            var separatorIndex = entry.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            result[entry.Substring(0, separatorIndex)] = entry.Substring(separatorIndex + 1);
        }

        return result;
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