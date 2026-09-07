using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging;
using NSubstitute;

namespace Amanhecer.Tests.Messaging;

public class AmanhecerPublicationFinderTests
{
    [Test]
    public async Task When_RoutingKeyIsRegistered_Should_ReturnThePublication()
    {
        var publication = Substitute.For<IPublication>();
        var finder = new AmanhecerPublicationFinder(new Dictionary<string, IPublication>
        {
            ["some.routing.key"] = publication
        }.ToFrozenDictionary());

        await Assert.That(finder.Find("some.routing.key")).IsEqualTo(publication);
    }

    [Test]
    public async Task When_RoutingKeyIsNotRegistered_Should_ThrowKeyNotFoundException()
    {
        var finder = new AmanhecerPublicationFinder(new Dictionary<string, IPublication>().ToFrozenDictionary());

        await Assert.That(() => finder.Find("unknown.routing.key"))
            .Throws<KeyNotFoundException>();
    }
}
