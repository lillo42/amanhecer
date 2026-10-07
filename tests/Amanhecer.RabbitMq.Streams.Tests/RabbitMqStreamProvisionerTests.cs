using System;
using System.Reflection;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.RabbitMq.Streams.Configurations;
using Amanhecer.RabbitMq.Streams.Provisioners;
using NSubstitute;
using RabbitMQ.Stream.Client;

namespace Amanhecer.RabbitMq.Streams.Tests;

/// <summary>
/// Broker-independent tests for the stream provisioners and the <see cref="CreateStreamConfigurator"/>:
/// the type guards that run before any broker call, and the mapping from the configurator to the
/// <see cref="CreateStream"/> provisioner.
/// </summary>
public class RabbitMqStreamProvisionerTests
{
    [Test]
    public async Task When_Assuming_A_Stream_Exists_Should_Complete_For_A_Publication()
    {
        var provisioner = new AssumeStreamExists();

        await Assert
            .That(async () => await provisioner.ExecuteAsync(
                Substitute.For<IGateway>(),
                new RabbitMqStreamPublication("tests.stream") { RoutingKey = "tests" }))
            .ThrowsNothing();
    }

    [Test]
    public async Task When_Assuming_A_Stream_Exists_Should_Complete_For_A_Subscription()
    {
        var provisioner = new AssumeStreamExists();

        await Assert
            .That(async () => await provisioner.ExecuteAsync(
                Substitute.For<IGateway>(),
                new RabbitMqStreamSubscription("tests", "tests.stream")))
            .ThrowsNothing();
    }

    [Test]
    public async Task When_Creating_A_Stream_For_A_Non_Stream_Publication_Should_Throw()
    {
        var provisioner = new CreateStream();

        await Assert
            .That(async () => await provisioner.ExecuteAsync(
                Substitute.For<IGateway>(),
                new TestPublication { RoutingKey = "tests" }))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Creating_A_Stream_For_A_Non_Stream_Subscription_Should_Throw()
    {
        var provisioner = new CreateStream();

        await Assert
            .That(async () => await provisioner.ExecuteAsync(
                Substitute.For<IGateway>(),
                new TestSubscription("tests")))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Creating_A_Stream_With_A_Non_Stream_Gateway_Should_Throw()
    {
        var provisioner = new CreateStream();

        await Assert
            .That(async () => await provisioner.ExecuteAsync(
                Substitute.For<IGateway>(),
                new RabbitMqStreamPublication("tests.stream") { RoutingKey = "tests" }))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Validating_A_Stream_For_A_Non_Stream_Publication_Should_Throw()
    {
        var provisioner = new ValidateStreamExists();

        await Assert
            .That(async () => await provisioner.ExecuteAsync(
                Substitute.For<IGateway>(),
                new TestPublication { RoutingKey = "tests" }))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Validating_A_Stream_For_A_Non_Stream_Subscription_Should_Throw()
    {
        var provisioner = new ValidateStreamExists();

        await Assert
            .That(async () => await provisioner.ExecuteAsync(
                Substitute.For<IGateway>(),
                new TestSubscription("tests")))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Validating_A_Stream_With_A_Non_Stream_Gateway_Should_Throw()
    {
        var provisioner = new ValidateStreamExists();

        await Assert
            .That(async () => await provisioner.ExecuteAsync(
                Substitute.For<IGateway>(),
                new RabbitMqStreamSubscription("tests", "tests.stream")))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Setting_A_Non_Positive_Max_Age_Should_Throw()
    {
        var configurator = new CreateStreamConfigurator();

        await Assert
            .That(() => configurator.MaxAge(TimeSpan.Zero))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task When_Setting_A_Non_Positive_Max_Segment_Size_Should_Throw()
    {
        var configurator = new CreateStreamConfigurator();

        await Assert
            .That(() => configurator.MaxSegmentSizeBytes(0))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task When_Building_The_Provisioner_Should_Carry_The_Configured_Values()
    {
        var configurator = new CreateStreamConfigurator();
        configurator
            .MaxAge(TimeSpan.FromHours(1))
            .MaxLengthBytes(2048)
            .MaxSegmentSizeBytes(512)
            .LeaderLocator(LeaderLocator.Random);

        var provisioner = BuildProvisioner(configurator);

        await Assert.That(provisioner.MaxAge).IsEqualTo(TimeSpan.FromHours(1));
        await Assert.That(provisioner.MaxLengthBytes).IsEqualTo(2048ul);
        await Assert.That(provisioner.MaxSegmentSizeBytes).IsEqualTo(512);
        await Assert.That(provisioner.LeaderLocator).IsEqualTo(LeaderLocator.Random);
    }

    private static CreateStream BuildProvisioner(CreateStreamConfigurator configurator)
    {
        var toProvisioner = typeof(CreateStreamConfigurator).GetMethod(
            "ToProvisioner",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        return (CreateStream)toProvisioner.Invoke(configurator, null)!;
    }

    private sealed class TestPublication : Publication;

    private sealed class TestSubscription(string toRoutingKey) : Subscription(toRoutingKey);
}
