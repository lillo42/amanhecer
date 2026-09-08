using System.Threading.Tasks;
using Amanhecer.Messaging.Base.Tests;

namespace Amanhecer.RabbitMq.Tests;

/// <summary>
/// Runs the transport-agnostic messaging gateway contract tests against RabbitMQ.
/// Requires a broker (see docker-compose-rabbitmq.yaml at the repository root).
/// </summary>
[InheritsTests]
public class RabbitMqMessagingGatewayTests : MessagingGatewayTests
{
    /// <inheritdoc />
    protected override Task<MessagingGatewayFixture> CreateFixtureAsync()
    {
        return RabbitMqMessagingGatewayFixture.CreateAsync();
    }
}
