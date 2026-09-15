using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Dekaf;
using Amanhecer.Dekaf.Provisioners;
using NSubstitute;

namespace Amanhecer.Dekaf.Tests;

/// <summary>
/// Broker-free tests for the topic provisioners: declaration-type validation and the
/// no-op provisioner.
/// </summary>
public class DekafProvisionerValidationTests
{
    private static DekafPublication CreatePublication()
    {
        return new DekafPublication { RoutingKey = "tests", Topic = "tests.topic" };
    }

    private static DekafSubscription CreateSubscription()
    {
        return new DekafSubscription("tests", "tests.topic", "tests-group");
    }

    [Test]
    public async Task CreateTopic_When_Gateway_Is_Not_DekafGateway_Should_Throw()
    {
        var provisioner = new CreateTopic();

        await Assert.That(async () => await provisioner.ExecuteAsync(
                Substitute.For<IGateway>(), CreatePublication()))
            .Throws<ArgumentException>();

        await Assert.That(async () => await provisioner.ExecuteAsync(
                Substitute.For<IGateway>(), CreateSubscription()))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task CreateTopic_When_Declaration_Has_The_Wrong_Type_Should_Throw()
    {
        var provisioner = new CreateTopic();
        var gateway = new DekafGateway();

        await Assert.That(async () => await provisioner.ExecuteAsync(
                gateway, Substitute.For<IPublication>()))
            .Throws<ArgumentException>();

        await Assert.That(async () => await provisioner.ExecuteAsync(
                gateway, Substitute.For<ISubscription>()))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task ValidateTopicExists_When_Gateway_Is_Not_DekafGateway_Should_Throw()
    {
        var provisioner = new ValidateTopicExists();

        await Assert.That(async () => await provisioner.ExecuteAsync(
                Substitute.For<IGateway>(), CreatePublication()))
            .Throws<ArgumentException>();

        await Assert.That(async () => await provisioner.ExecuteAsync(
                Substitute.For<IGateway>(), CreateSubscription()))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task ValidateTopicExists_When_Declaration_Has_The_Wrong_Type_Should_Throw()
    {
        var provisioner = new ValidateTopicExists();
        var gateway = new DekafGateway();

        await Assert.That(async () => await provisioner.ExecuteAsync(
                gateway, Substitute.For<IPublication>()))
            .Throws<ArgumentException>();

        await Assert.That(async () => await provisioner.ExecuteAsync(
                gateway, Substitute.For<ISubscription>()))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task AssumeTopicExists_Should_Do_Nothing()
    {
        var provisioner = new AssumeTopicExists();

        await provisioner.ExecuteAsync(Substitute.For<IGateway>(), CreatePublication());
        await provisioner.ExecuteAsync(Substitute.For<IGateway>(), CreateSubscription());
    }
}
