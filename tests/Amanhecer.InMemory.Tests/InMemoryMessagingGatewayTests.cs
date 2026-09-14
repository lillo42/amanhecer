using System.Threading.Tasks;
using Amanhecer.Messaging.Base.Tests;

namespace Amanhecer.InMemory.Tests;

[InheritsTests]
public class InMemoryMessagingGatewayTests : MessagingGatewayTests
{
    protected override Task<MessagingGatewayFixture> CreateFixtureAsync()
    {
        return InMemoryMessagingGatewayFixture.CreateAsync();
    }
}
