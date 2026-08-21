using System.Collections.Generic;
using System.Linq;

namespace Amanhecer.Abstractions.Messaging;

public class Baggage : Dictionary<string, string?>
{
    public Baggage()
    {
    }

    public Baggage(IDictionary<string, string?> items) : base(items)
    {
    }


#if NETFRAMEWORK || NETSTANDARD
    public Baggage(IEnumerable<KeyValuePair<string, string?>> items)
    {
        foreach (var item in items)
        {
            Add(item.Key, item.Value);
        }
    }
#else
    public Baggage(IEnumerable<KeyValuePair<string, string?>> items) : base(items)
    {
    }
#endif

    public static Baggage FromString(string baggage)
    {
        var dict = baggage
            .Split(',')
            .Select(x => x.Split('='))
            .Where(x => x.Length == 2)
            .ToDictionary(x => x[0], x => x[1]);

        return new Baggage(dict);
    }


    public override string ToString()
    {
        return string.Join(",", this
            .Where(x => x.Value != null)
            .Select(x => $"{x.Key}={x.Value}"));
    }
}