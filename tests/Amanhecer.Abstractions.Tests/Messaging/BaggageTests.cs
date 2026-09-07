using System.Collections.Generic;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Abstractions.Tests.Messaging;

public class BaggageTests
{
    [Test]
    public async Task FromString_Should_ParseKeyValuePairs()
    {
        var baggage = Baggage.FromString("userId=alice,serverNode=DF28");

        await Assert.That(baggage.Count).IsEqualTo(2);
        await Assert.That(baggage["userId"]).IsEqualTo("alice");
        await Assert.That(baggage["serverNode"]).IsEqualTo("DF28");
    }

    [Test]
    public async Task FromString_ValueContainingEquals_Should_KeepRemainderAsValue()
    {
        var baggage = Baggage.FromString("key=a=b=c");

        await Assert.That(baggage["key"]).IsEqualTo("a=b=c");
    }

    [Test]
    public async Task FromString_EmptyString_Should_ReturnEmptyBaggage()
    {
        var baggage = Baggage.FromString("");

        await Assert.That(baggage).IsEmpty();
    }

    [Test]
    public async Task FromString_InvalidEntries_Should_BeIgnored()
    {
        var baggage = Baggage.FromString("nokey,novalue,=leadingsep,valid=1");

        await Assert.That(baggage.Count).IsEqualTo(1);
        await Assert.That(baggage["valid"]).IsEqualTo("1");
    }

    [Test]
    public async Task FromString_DuplicateKeys_Should_LetLastValueWin()
    {
        var baggage = Baggage.FromString("key=first,key=last");

        await Assert.That(baggage.Count).IsEqualTo(1);
        await Assert.That(baggage["key"]).IsEqualTo("last");
    }

    [Test]
    public async Task ToString_Should_SerializeAsCommaSeparatedPairs()
    {
        var baggage = new Baggage
        {
            ["userId"] = "alice",
            ["serverNode"] = "DF28"
        };

        var serialized = baggage.ToString();

        await Assert.That(serialized).IsEqualTo("userId=alice,serverNode=DF28");
    }

    [Test]
    public async Task ToString_Should_SkipNullValues()
    {
        var baggage = new Baggage
        {
            ["userId"] = "alice",
            ["missing"] = null
        };

        var serialized = baggage.ToString();

        await Assert.That(serialized).IsEqualTo("userId=alice");
    }

    [Test]
    public async Task ToString_EmptyBaggage_Should_ReturnEmptyString()
    {
        var baggage = new Baggage();

        await Assert.That(baggage.ToString()).IsEqualTo("");
    }

    [Test]
    public async Task RoundTrip_Should_PreserveEntries()
    {
        var original = "userId=alice,serverNode=DF28";

        var serialized = Baggage.FromString(original).ToString();

        await Assert.That(serialized).IsEqualTo(original);
    }

    [Test]
    public async Task DictionaryConstructor_Should_CopyItems()
    {
        var items = new Dictionary<string, string?> { ["a"] = "1", ["b"] = null };

        var baggage = new Baggage(items);

        await Assert.That(baggage.Count).IsEqualTo(2);
        await Assert.That(baggage["a"]).IsEqualTo("1");
        await Assert.That(baggage["b"]).IsNull();
    }
}
