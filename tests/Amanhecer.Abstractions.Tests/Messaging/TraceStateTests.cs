using System.Collections.Generic;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Abstractions.Tests.Messaging;

public class TraceStateTests
{
    [Test]
    public async Task FromString_Should_ParseKeyValuePairs()
    {
        var traceState = TraceState.FromString("congo=ucf=ifqn5fnsiO,rojo=00f067aa0ba902b7");

        await Assert.That(traceState.Count).IsEqualTo(2);
        await Assert.That(traceState["congo"]).IsEqualTo("ucf=ifqn5fnsiO");
        await Assert.That(traceState["rojo"]).IsEqualTo("00f067aa0ba902b7");
    }

    [Test]
    public async Task FromString_EmptyString_Should_ReturnEmptyTraceState()
    {
        var traceState = TraceState.FromString("");

        await Assert.That(traceState).IsEmpty();
    }

    [Test]
    public async Task FromString_InvalidEntries_Should_BeIgnored()
    {
        var traceState = TraceState.FromString("nokey,novalue,=leadingsep,valid=1");

        await Assert.That(traceState.Count).IsEqualTo(1);
        await Assert.That(traceState["valid"]).IsEqualTo("1");
    }

    [Test]
    public async Task FromString_DuplicateKeys_Should_LetLastValueWin()
    {
        var traceState = TraceState.FromString("key=first,key=last");

        await Assert.That(traceState.Count).IsEqualTo(1);
        await Assert.That(traceState["key"]).IsEqualTo("last");
    }

    [Test]
    public async Task ToString_Should_SerializeAsCommaSeparatedPairs()
    {
        var traceState = new TraceState
        {
            ["congo"] = "ucf=ifqn5fnsiO",
            ["rojo"] = "00f067aa0ba902b7"
        };

        var serialized = traceState.ToString();

        await Assert.That(serialized).IsEqualTo("congo=ucf=ifqn5fnsiO,rojo=00f067aa0ba902b7");
    }

    [Test]
    public async Task ToString_EmptyTraceState_Should_ReturnEmptyString()
    {
        var traceState = new TraceState();

        await Assert.That(traceState.ToString()).IsEqualTo("");
    }

    [Test]
    public async Task RoundTrip_Should_PreserveEntries()
    {
        var original = "congo=ucf=ifqn5fnsiO,rojo=00f067aa0ba902b7";

        var serialized = TraceState.FromString(original).ToString();

        await Assert.That(serialized).IsEqualTo(original);
    }

    [Test]
    public async Task DictionaryConstructor_Should_CopyItems()
    {
        var items = new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" };

        var traceState = new TraceState(items);

        await Assert.That(traceState.Count).IsEqualTo(2);
        await Assert.That(traceState["a"]).IsEqualTo("1");
        await Assert.That(traceState["b"]).IsEqualTo("2");
    }
}
