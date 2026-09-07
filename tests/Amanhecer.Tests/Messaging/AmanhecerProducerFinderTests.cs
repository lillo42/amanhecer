using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging;
using NSubstitute;

namespace Amanhecer.Tests.Messaging;

public class AmanhecerProducerFinderTests
{
    [Test]
    public async Task When_RoutingKeyIsRegistered_Should_ReturnTheProducer()
    {
        var producer = Substitute.For<IProducer>();
        var finder = new AmanhecerProducerFinder(new Dictionary<string, IProducer>
        {
            ["some.routing.key"] = producer
        }.ToFrozenDictionary());

        await Assert.That(finder.Find("some.routing.key")).IsEqualTo(producer);
    }

    [Test]
    public async Task When_RoutingKeyIsNotRegistered_Should_ThrowKeyNotFoundException()
    {
        var finder = new AmanhecerProducerFinder(new Dictionary<string, IProducer>().ToFrozenDictionary());

        await Assert.That(() => finder.Find("unknown.routing.key"))
            .Throws<KeyNotFoundException>();
    }
}
