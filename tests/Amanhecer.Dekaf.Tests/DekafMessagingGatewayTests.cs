using System;
using System.Threading.Tasks;
using Amanhecer.Messaging.Base.Tests;

namespace Amanhecer.Dekaf.Tests;

/// <summary>
/// Runs the transport-agnostic messaging gateway contract tests against Kafka through the
/// Dekaf transport. Requires a broker (see docker-compose-kafka.yaml at the repository
/// root).
/// </summary>
[InheritsTests]
public class DekafMessagingGatewayTests : MessagingGatewayTests
{
    // The first consume of a fresh consumer group joins the group and rebalances, which can
    // take several seconds on a cold broker.
    /// <inheritdoc />
    protected override TimeSpan ReceiveTimeout => TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    protected override Task<MessagingGatewayFixture> CreateFixtureAsync()
    {
        return KafkaMessagingGatewayFixture.CreateAsync();
    }
}
