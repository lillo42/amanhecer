using System;
using System.Linq;
using System.Threading.Tasks;
using Amanhencer.Abstractions;

namespace Amanhencer.Tests;

public class UuidTests
{
    [Test]
    public async Task NewGuid_ReturnsNonEmptyGuid()
    {
        var guid = Uuid.NewGuid();

        await Assert.That(guid).IsNotEqualTo(Guid.Empty);
    }

    [Test]
    public async Task NewGuid_ReturnsUniqueValues()
    {
        var guids = Enumerable.Range(0, 100).Select(_ => Uuid.NewGuid()).ToHashSet();

        await Assert.That(guids.Count).IsEqualTo(100);
    }

    [Test]
    public async Task NewGuid_ReturnsVersion7Guid()
    {
        var guid = Uuid.NewGuid();

        await Assert.That(guid.Version).IsEqualTo(7);
    }
}
