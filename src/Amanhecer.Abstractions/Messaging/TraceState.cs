using System.Collections.Generic;
using System.Linq;

namespace Amanhecer.Abstractions.Messaging;

public class TraceState : Dictionary<string, string>
{
    public TraceState()
    {
    }

    public TraceState(IDictionary<string, string> items) : base(items)
    {
    }

    public static TraceState FromString(string baggage)
    {
        var dict = baggage
            .Split(',')
            .Select(x => x.Split('='))
            .Where(x => x.Length == 2)
            .ToDictionary(x => x[0], x => x[1]);

        return new TraceState(dict);
    }

    public override string ToString()
    {
        return string.Join(",", this.Select(x => $"{x.Key}={x.Value}"));
    }
}