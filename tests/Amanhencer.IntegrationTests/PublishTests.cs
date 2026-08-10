using System;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.ExecutingStrategies;

namespace Amanhencer.IntegrationTests;

public class PublishTests
{
    [Test]
    public async Task PublishAsync_MultipleHandlers_EachRunsThroughItsOwnPipeline()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg =>
        {
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing
                .Use<OuterMiddleware>(order: 1)
                .UseHandler<FirstShippedHandler>());
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing
                .Use<InnerMiddleware>(order: 1)
                .UseHandler<SecondShippedHandler>());
        });

        await dispatcher.PublishAsync(new OrderShipped("book"));

        await Assert.That(log.Joined())
            .IsEqualTo("outer:before,first:book,outer:after,inner:before,second:book,inner:after");
    }

    [Test]
    public async Task Publish_SyncOverload_RunsAllHandlers()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg =>
        {
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing.UseHandler<FirstShippedHandler>());
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing.UseHandler<SecondShippedHandler>());
        });

        dispatcher.Publish(new OrderShipped("book"));

        await Assert.That(log.Entries).Contains("first:book");
        await Assert.That(log.Entries).Contains("second:book");
    }

    [Test]
    public async Task PublishAsync_WithExplicitContext_RunsAllHandlers()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg =>
        {
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing.UseHandler<FirstShippedHandler>());
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing.UseHandler<SecondShippedHandler>());
        });

        await dispatcher.PublishAsync(new OrderShipped("book"), new AmanhencerContext());

        await Assert.That(log.Entries).Contains("first:book");
        await Assert.That(log.Entries).Contains("second:book");
    }

    [Test]
    public async Task PublishAsync_NoSubscribers_CompletesWithoutError()
    {
        var (dispatcher, log) = DispatcherFixture.Create(_ => { });

        await Assert.That(async () => await dispatcher.PublishAsync(new OrderShipped("book")))
            .ThrowsNothing();
        await Assert.That(log.Entries).IsEmpty();
    }

    [Test]
    public async Task PublishAsync_SubscriberThrows_RemainingHandlersRunAndAggregateExceptionThrown()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg =>
        {
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing.UseHandler<FirstShippedHandler>());
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing.UseHandler<ExplodingShippedHandler>());
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing.UseHandler<SecondShippedHandler>());
        });

        // The default SequenceExecutingStrategy keeps running the remaining pipelines after a
        // failure, collecting every exception, and throws a single AggregateException at the end.
        var exception = await Assert.That(async () => await dispatcher.PublishAsync(new OrderShipped("book")))
            .ThrowsExactly<AggregateException>();

        await Assert.That(log.Entries).Contains("first:book");
        await Assert.That(log.Entries).Contains("exploding:book");
        await Assert.That(log.Entries).Contains("second:book");
        await Assert.That(exception!.InnerExceptions.Count).IsEqualTo(1);
        await Assert.That(exception.InnerExceptions[0] is InvalidOperationException).IsTrue();
    }

    [Test]
    public async Task PublishAsync_ParallelStrategy_SubscriberThrows_SkipsUnstartedPipelinesAndRethrows()
    {
        var (dispatcher, log) = DispatcherFixture.Create(cfg =>
        {
            cfg.SetExecutorStrategy(new ParallelExecutingStrategy(new ParallelOptions { MaxDegreeOfParallelism = 1 }));
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing.UseHandler<ExplodingShippedHandler>());
            cfg.AddRoutingKey(DispatcherFixture.KeyOf<OrderShipped>(), routing => routing.UseHandler<FirstShippedHandler>());
        });

        // Pinned behavior (MaxDegreeOfParallelism = 1 keeps it deterministic): once a pipeline
        // faults, Parallel.ForEachAsync stops scheduling new iterations, so the second pipeline
        // never runs; and a single failure propagates unwrapped — no AggregateException, unlike
        // the SequenceExecutingStrategy.
        var exception = await Assert.That(async () => await dispatcher.PublishAsync(new OrderShipped("book")))
            .ThrowsExactly<InvalidOperationException>();

        await Assert.That(exception!.Message).IsEqualTo("Subscriber exploded.");
        await Assert.That(log.Joined()).IsEqualTo("exploding:book");
    }
}
