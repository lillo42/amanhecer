using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Amanhecer.Extensions.Hosting.Tests;

public class ConsumerHostedServiceTests
{
    private static ISubscription CreateSubscription(int numberOfConsumers)
    {
        var subscription = Substitute.For<ISubscription>();
        subscription.NumberOfConsumers.Returns(numberOfConsumers);
        return subscription;
    }

    private static IGateway CreateGateway(params ISubscription[] subscriptions)
    {
        var gateway = Substitute.For<IGateway>();
        gateway.Subscriptions.Returns(subscriptions);
        gateway.CreateConsumer(Arg.Any<ISubscription>()).Returns(_ => Substitute.For<IConsumer>());
        return gateway;
    }

    private static ServiceProvider CreateProvider(params IGateway[] gateways)
    {
        var services = new ServiceCollection();
        foreach (var gateway in gateways)
        {
            services.AddSingleton(gateway);
        }

        return services.BuildServiceProvider();
    }

    private static IMessagePumper CreatePump(Func<IConsumer, CancellationToken, Task>? execute = null)
    {
        var pump = Substitute.For<IMessagePumper>();
        pump.ExecuteAsync(Arg.Any<IConsumer>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
                execute?.Invoke(callInfo.Arg<IConsumer>(), callInfo.Arg<CancellationToken>())
                ?? Task.CompletedTask);
        return pump;
    }

    [Test]
    public async Task When_StartAsync_WhenNoGatewayIsRegistered_Should_NotCreateAnyPump()
    {
        var pumperFactory = Substitute.For<IMessagePumperFactory>();
        using var provider = CreateProvider();
        var service = new ConsumerHostedService(provider, pumperFactory);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        pumperFactory.DidNotReceive().Create();
    }

    [Test]
    public async Task When_StartAsync_Should_CreateOnePumpPerConsumerOfEverySubscription()
    {
        var firstSubscription = CreateSubscription(numberOfConsumers: 2);
        var secondSubscription = CreateSubscription(numberOfConsumers: 3);
        var firstGateway = CreateGateway(firstSubscription, secondSubscription);
        var thirdSubscription = CreateSubscription(numberOfConsumers: 1);
        var secondGateway = CreateGateway(thirdSubscription);
        var pumperFactory = Substitute.For<IMessagePumperFactory>();
        pumperFactory.Create().Returns(_ => CreatePump());
        using var provider = CreateProvider(firstGateway, secondGateway);
        var service = new ConsumerHostedService(provider, pumperFactory);

        await service.StartAsync(CancellationToken.None);

        pumperFactory.Received(6).Create();
        firstGateway.Received(2).CreateConsumer(firstSubscription);
        firstGateway.Received(3).CreateConsumer(secondSubscription);
        secondGateway.Received(1).CreateConsumer(thirdSubscription);
    }

    [Test]
    public async Task When_StartAsync_WhenSubscriptionHasNoConsumers_Should_NotCreateAnyPumpForIt()
    {
        var subscription = CreateSubscription(numberOfConsumers: 0);
        var gateway = CreateGateway(subscription);
        var pumperFactory = Substitute.For<IMessagePumperFactory>();
        using var provider = CreateProvider(gateway);
        var service = new ConsumerHostedService(provider, pumperFactory);

        await service.StartAsync(CancellationToken.None);

        pumperFactory.DidNotReceive().Create();
        gateway.DidNotReceive().CreateConsumer(Arg.Any<ISubscription>());
    }

    [Test]
    public async Task When_StartAsync_Should_PumpTheConsumerCreatedForEachSubscription()
    {
        var subscription = CreateSubscription(numberOfConsumers: 1);
        var gateway = Substitute.For<IGateway>();
        gateway.Subscriptions.Returns([subscription]);
        var consumer = Substitute.For<IConsumer>();
        gateway.CreateConsumer(subscription).Returns(consumer);

        IConsumer? pumpedConsumer = null;
        CancellationToken pumpedToken = default;
        var pump = CreatePump((pumped, token) =>
        {
            pumpedConsumer = pumped;
            pumpedToken = token;
            return Task.CompletedTask;
        });
        var pumperFactory = Substitute.For<IMessagePumperFactory>();
        pumperFactory.Create().Returns(pump);
        using var provider = CreateProvider(gateway);
        var service = new ConsumerHostedService(provider, pumperFactory);

        await service.StartAsync(CancellationToken.None);

        await Assert.That(ReferenceEquals(pumpedConsumer, consumer)).IsTrue();
        await Assert.That(pumpedToken.CanBeCanceled).IsTrue();
        await Assert.That(pumpedToken.IsCancellationRequested).IsFalse();
    }

    [Test]
    public async Task When_StopAsync_WhenNeverStarted_Should_CompleteWithoutCreatingPumps()
    {
        var pumperFactory = Substitute.For<IMessagePumperFactory>();
        using var provider = CreateProvider(CreateGateway(CreateSubscription(numberOfConsumers: 1)));
        var service = new ConsumerHostedService(provider, pumperFactory);

        await service.StopAsync(CancellationToken.None);

        pumperFactory.DidNotReceive().Create();
    }

    [Test]
    public async Task When_StopAsync_Should_CancelThePumpTokens()
    {
        var subscription = CreateSubscription(numberOfConsumers: 1);
        var gateway = CreateGateway(subscription);

        var observedCancellation = false;
        var pump = CreatePump((_, token) =>
        {
            var completion = new TaskCompletionSource();
            token.Register(() =>
            {
                observedCancellation = true;
                completion.SetResult();
            });
            return completion.Task;
        });
        var pumperFactory = Substitute.For<IMessagePumperFactory>();
        pumperFactory.Create().Returns(pump);
        using var provider = CreateProvider(gateway);
        var service = new ConsumerHostedService(provider, pumperFactory);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        await Assert.That(observedCancellation).IsTrue();
    }

    [Test]
    public async Task When_StopAsync_Should_WaitForPumpTasksToComplete()
    {
        var subscription = CreateSubscription(numberOfConsumers: 1);
        var gateway = CreateGateway(subscription);

        var pumpCompletion = new TaskCompletionSource();
        var pump = CreatePump((_, _) => pumpCompletion.Task);
        var pumperFactory = Substitute.For<IMessagePumperFactory>();
        pumperFactory.Create().Returns(pump);
        using var provider = CreateProvider(gateway);
        var service = new ConsumerHostedService(provider, pumperFactory);

        await service.StartAsync(CancellationToken.None);

        var stopTask = service.StopAsync(CancellationToken.None);
        await Assert.That(stopTask.IsCompleted).IsFalse();

        pumpCompletion.SetResult();
        await stopTask;
    }

    [Test]
    public async Task When_StopAsync_WhenPumpTaskIsCancelled_Should_CompleteWithoutThrowing()
    {
        var subscription = CreateSubscription(numberOfConsumers: 1);
        var gateway = CreateGateway(subscription);
        var pump = CreatePump((_, _) => Task.FromCanceled(new CancellationToken(canceled: true)));
        var pumperFactory = Substitute.For<IMessagePumperFactory>();
        pumperFactory.Create().Returns(pump);
        using var provider = CreateProvider(gateway);
        var service = new ConsumerHostedService(provider, pumperFactory);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task When_StopAsync_WhenCalledTwice_Should_Complete()
    {
        var subscription = CreateSubscription(numberOfConsumers: 1);
        var gateway = CreateGateway(subscription);
        var pumperFactory = Substitute.For<IMessagePumperFactory>();
        pumperFactory.Create().Returns(_ => CreatePump());
        using var provider = CreateProvider(gateway);
        var service = new ConsumerHostedService(provider, pumperFactory);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task When_StartedAgainAfterStop_Should_RecreateConsumersAndPumps()
    {
        var subscription = CreateSubscription(numberOfConsumers: 1);
        var gateway = CreateGateway(subscription);
        var pumperFactory = Substitute.For<IMessagePumperFactory>();
        pumperFactory.Create().Returns(_ => CreatePump());
        using var provider = CreateProvider(gateway);
        var service = new ConsumerHostedService(provider, pumperFactory);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
        await service.StartAsync(CancellationToken.None);

        pumperFactory.Received(2).Create();
        gateway.Received(2).CreateConsumer(subscription);
    }
}
