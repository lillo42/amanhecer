using System.Collections.Generic;
using System.Linq;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// A set of string key/value pairs propagated alongside a message, following the W3C
/// Baggage specification. Values with a <see langword="null"/> value are dropped when
/// the baggage is serialized.
/// </summary>
public class Baggage : Dictionary<string, string?>
{
    /// <summary>
    /// Initializes a new, empty <see cref="Baggage"/> instance.
    /// </summary>
    public Baggage()
    {
    }

    /// <summary>
    /// Initializes a new <see cref="Baggage"/> instance from an existing dictionary.
    /// </summary>
    /// <param name="items">The key/value pairs to copy into the baggage.</param>
    public Baggage(Dictionary<string, string?> items) : base(items)
    {
    }


#if NETFRAMEWORK || NETSTANDARD
    /// <summary>
    /// Initializes a new <see cref="Baggage"/> instance from a sequence of key/value pairs.
    /// </summary>
    /// <param name="items">The key/value pairs to copy into the baggage.</param>
    public Baggage(IEnumerable<KeyValuePair<string, string?>> items)
    {
        foreach (var item in items)
        {
            Add(item.Key, item.Value);
        }
    }
#else
    /// <summary>
    /// Initializes a new <see cref="Baggage"/> instance from a sequence of key/value pairs.
    /// </summary>
    /// <param name="items">The key/value pairs to copy into the baggage.</param>
    public Baggage(IEnumerable<KeyValuePair<string, string?>> items) : base(items)
    {
    }
#endif

    /// <summary>
    /// Parses a W3C Baggage header value into a <see cref="Baggage"/> instance. Entries
    /// that are not in the <c>key=value</c> form are ignored.
    /// </summary>
    /// <param name="baggage">The serialized baggage, as a comma-separated list of
    /// <c>key=value</c> pairs.</param>
    /// <returns>The parsed <see cref="Baggage"/>.</returns>
    public static Baggage FromString(string baggage)
    {
        var dict = baggage
            .Split(',')
            .Select(x => x.Split('='))
            .Where(x => x.Length == 2)
            .ToDictionary(x => x[0], x => x[1]);

        return new Baggage(dict!);
    }


    /// <summary>
    /// Serializes the baggage into a W3C Baggage header value: a comma-separated list of
    /// <c>key=value</c> pairs, skipping entries whose value is <see langword="null"/>.
    /// </summary>
    /// <returns>The serialized baggage.</returns>
    public override string ToString()
    {
        return string.Join(",", this
            .Where(x => x.Value != null)
            .Select(x => $"{x.Key}={x.Value}"));
    }
}