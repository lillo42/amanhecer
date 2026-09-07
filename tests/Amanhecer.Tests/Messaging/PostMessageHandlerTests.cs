using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging.Handlers;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Amanhecer.Tests.Messaging;

public class PostMessageHandlerTests
{
    private const string RoutingKey = "orders";

    private readonly IProducerFinder _producerFinder = Substitute.For<IProducerFinder>();
    private readonly IPublicationFinder _publicationFinder = Substitute.For<IPublicationFinder>();
    private readonly PostMessageHandler _handler;

    public PostMessageHandlerTests()
    {
        _handler = new PostMessageHandler(_producerFinder, _publicationFinder);
    }

    private static AmanhecerContext CreateContext(string routingKey = RoutingKey)
    {
        var context = new AmanhecerContext();
        context.SetMetadata(routingKey, MetadataName.PublicationRoutingKey);
        return context;
    }

    [Test]
    public async Task HandleAsync_Should_ProduceTheMessageThroughTheResolvedPublicationAndProducer()
    {
        var publication = new TestPublication { RoutingKey = RoutingKey };
        _publicationFinder.Find(RoutingKey).Returns(publication);
        var producer = Substitute.For<IProducer>();
        _producerFinder.Find(RoutingKey).Returns(producer);
        var context = CreateContext();
        var message = new Message();

        await _handler.HandleAsync(message, context);

        _publicationFinder.Received(1).Find(RoutingKey);
        _producerFinder.Received(1).Find(RoutingKey);
        await producer.Received(1).ProduceAsync(message, publication, context);
    }

    [Test]
    public async Task HandleAsync_When_PublicationRoutingKeyMetadataMissing_Should_ThrowKeyNotFoundException()
    {
        await Assert.That(async () => await _handler.HandleAsync(new Message(), new AmanhecerContext()))
            .Throws<KeyNotFoundException>();

        _publicationFinder.DidNotReceive().Find(Arg.Any<string>());
        _producerFinder.DidNotReceive().Find(Arg.Any<string>());
    }

    [Test]
    public async Task HandleAsync_When_NoPublicationConfigured_Should_ThrowInvalidOperationException()
    {
        _publicationFinder.Find(RoutingKey).Throws(new KeyNotFoundException());
        var context = CreateContext();

        Exception? caught = null;
        try
        {
            await _handler.HandleAsync(new Message(), context);
        }
        catch (Exception exception)
        {
            caught = exception;
        }

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(caught!.Message.Contains(RoutingKey)).IsTrue();
        await Assert.That(caught.InnerException).IsTypeOf<KeyNotFoundException>();
        _producerFinder.DidNotReceive().Find(Arg.Any<string>());
    }
}
