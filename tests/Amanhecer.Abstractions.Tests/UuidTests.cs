using System;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Tests;

public class UuidTests
{
    [Test]
    public async Task NewGuid_Should_ReturnNonEmptyGuid()
    {
        var guid = Uuid.NewGuid();

        await Assert.That(guid).IsNotEqualTo(Guid.Empty);
    }

    [Test]
    public async Task NewGuid_Should_ReturnUniqueGuids()
    {
        var first = Uuid.NewGuid();
        var second = Uuid.NewGuid();

        await Assert.That(second).IsNotEqualTo(first);
    }

    [Test]
    public async Task NewGuid_Should_ReturnVersion7Guid()
    {
        var guid = Uuid.NewGuid();

        await Assert.That(guid.Version).IsEqualTo(7);
    }
}
