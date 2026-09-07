using System.Threading.Tasks;
using Amanhecer.Abstractions.Metadatas;

namespace Amanhecer.Abstractions.Tests.Metadatas;

public class HandleTypeMetadataTests
{
    [Test]
    public async Task Constructor_Should_StoreHandlerType()
    {
        var metadata = new HandleTypeMetadata(typeof(string));

        await Assert.That(metadata.HandlerType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task RecordsWithSameHandlerType_Should_BeEqual()
    {
        var first = new HandleTypeMetadata(typeof(int));
        var second = new HandleTypeMetadata(typeof(int));

        await Assert.That(first).IsEqualTo(second);
        await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
    }

    [Test]
    public async Task RecordsWithDifferentHandlerTypes_Should_NotBeEqual()
    {
        var first = new HandleTypeMetadata(typeof(int));
        var second = new HandleTypeMetadata(typeof(string));

        await Assert.That(first).IsNotEqualTo(second);
    }
}
