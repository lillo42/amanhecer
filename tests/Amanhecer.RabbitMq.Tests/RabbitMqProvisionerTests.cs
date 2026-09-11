using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.RabbitMq.Provisioners;
using NSubstitute;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq.Tests;

/// <summary>
/// Broker-independent tests for the exchange provisioners, asserting the channel calls each
/// one makes through a mocked <see cref="IChannel"/>.
/// </summary>
public class RabbitMqProvisionerTests
{
    [Test]
    public async Task When_Creating_An_Exchange_If_Not_Exists_Should_Declare_It()
    {
        var channel = Substitute.For<IChannel>();
        var provisioner = new CreateIfNotExchange
        {
            Type = ExchangeType.Topic,
            Durable = true,
            AutoDelete = true,
            Arguments = new Dictionary<string, object?> { ["alternate-exchange"] = "tests.alternate" }
        };
        var exchange = new Exchange { Name = "tests.exchange", Provisioner = provisioner };

        await provisioner.ExecuteAsync(channel, exchange);

        await channel.Received(1).ExchangeDeclareAsync(
            "tests.exchange",
            ExchangeType.Topic,
            true,
            true,
            Arg.Is<IDictionary<string, object?>?>(arguments =>
                arguments != null && Equals(arguments["alternate-exchange"], "tests.alternate")),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task When_Validating_An_Exchange_Should_Declare_It_Passively()
    {
        var channel = Substitute.For<IChannel>();
        var provisioner = new ValidateExchangeExists();
        var exchange = new Exchange { Name = "tests.exchange", Provisioner = provisioner };

        await provisioner.ExecuteAsync(channel, exchange);

        await channel.Received(1).ExchangeDeclarePassiveAsync(
            "tests.exchange",
            Arg.Any<CancellationToken>());
        await channel.DidNotReceive().ExchangeDeclareAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<IDictionary<string, object?>?>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task When_Assuming_An_Exchange_Exists_Should_Not_Touch_The_Channel()
    {
        var channel = Substitute.For<IChannel>();
        var provisioner = new AssumeExchangeExists();
        var exchange = new Exchange { Name = "tests.exchange", Provisioner = provisioner };

        await provisioner.ExecuteAsync(channel, exchange);

        await Assert.That(channel.ReceivedCalls().Count()).IsEqualTo(0);
    }
}
