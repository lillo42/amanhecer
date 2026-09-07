using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging.Middlewares;
using NSubstitute;

namespace Amanhecer.Tests.Messaging;

public class EncodeMiddlewareTests
{
    private readonly IEncodeTransformerPipelineFactory _transformerPipelineFactory;
    private readonly IMessageMapperFactory _messageMapperFactory;
    private readonly IPublicationFinder _publicationFinder;
    private readonly EncodeMiddleware _middleware;

    public EncodeMiddlewareTests()
    {
        _transformerPipelineFactory = Substitute.For<IEncodeTransformerPipelineFactory>();
        _messageMapperFactory = Substitute.For<IMessageMapperFactory>();
        _publicationFinder = Substitute.For<IPublicationFinder>();
        _middleware = new EncodeMiddleware(_transformerPipelineFactory, _messageMapperFactory, _publicationFinder);
    }

    [Test]
    public async Task When_RequestIsAlreadyAMessage_Should_PassThroughUnchanged()
    {
        var message = new Message();
        var context = new AmanhecerContext { Request = message };
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        await _middleware.ExecuteAsync(context, next);

        await next.Received(1).Invoke(context);
        await Assert.That(context.Request).IsEqualTo(message);
        _publicationFinder.DidNotReceive().Find(Arg.Any<string>());
        _transformerPipelineFactory.DidNotReceive().Create(Arg.Any<string>(), Arg.Any<AmanhecerContext>());
        _messageMapperFactory.DidNotReceive().Create(Arg.Any<Type>());
    }

    [Test]
    public async Task When_PublicationInMetadata_Should_MapEncodeAndReplaceRequest()
    {
        var publication = Substitute.For<IPublication>();
        publication.Name.Returns("some-publication");
        publication.MessageMapperType.Returns(typeof(SomeMessageMapper));

        var request = new SomeRequest();
        var message = new Message();
        var context = new AmanhecerContext { Request = request };
        context.SetMetadata(publication, MetadataName.Publication);

        var mapper = Substitute.For<IMessageMapper>();
        mapper.ToMessageAsync(request, context).Returns(new ValueTask<Message>(message));
        _messageMapperFactory.Create(typeof(SomeMessageMapper)).Returns(mapper);

        var pipeline = Substitute.For<IEncodeTransformerPipeline>();
        _transformerPipelineFactory
            .Create("Amanhecer.Messaging.Transformer.Encode.some-publication", context)
            .Returns(pipeline);

        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        await _middleware.ExecuteAsync(context, next);

        _publicationFinder.DidNotReceive().Find(Arg.Any<string>());
        _messageMapperFactory.Received(1).Create(typeof(SomeMessageMapper));
        await mapper.Received(1).ToMessageAsync(request, context);
        _transformerPipelineFactory
            .Received(1)
            .Create("Amanhecer.Messaging.Transformer.Encode.some-publication", context);
        await pipeline.Received(1).EncodeAsync(message, context);

        await Assert.That(context.Request).IsEqualTo(message);
        await Assert.That(context.GetMetadata<object>(MetadataName.OriginalRequest)).IsEqualTo(request);

        await next.Received(1).Invoke(context);
    }

    [Test]
    public async Task When_MessageMapperTypeInMetadata_Should_TakePrecedenceOverPublicationMapperType()
    {
        var publication = Substitute.For<IPublication>();
        publication.Name.Returns("some-publication");
        publication.MessageMapperType.Returns(typeof(SomeMessageMapper));

        var request = new SomeRequest();
        var message = new Message();
        var context = new AmanhecerContext { Request = request };
        context.SetMetadata(publication, MetadataName.Publication);
        context.SetMetadata(typeof(AnotherMessageMapper), MetadataName.MessageMapperType);

        var mapper = Substitute.For<IMessageMapper>();
        mapper.ToMessageAsync(request, context).Returns(new ValueTask<Message>(message));
        _messageMapperFactory.Create(typeof(AnotherMessageMapper)).Returns(mapper);

        var pipeline = Substitute.For<IEncodeTransformerPipeline>();
        _transformerPipelineFactory
            .Create(Arg.Any<string>(), Arg.Any<AmanhecerContext>())
            .Returns(pipeline);

        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        await _middleware.ExecuteAsync(context, next);

        _messageMapperFactory.Received(1).Create(typeof(AnotherMessageMapper));
        _messageMapperFactory.DidNotReceive().Create(typeof(SomeMessageMapper));

        await Assert.That(context.Request).IsEqualTo(message);

        await next.Received(1).Invoke(context);
    }

    [Test]
    public async Task When_NoPublicationInMetadata_Should_ResolveViaPublicationRoutingKeyMetadata()
    {
        var publication = Substitute.For<IPublication>();
        publication.Name.Returns("resolved-publication");
        publication.MessageMapperType.Returns(typeof(SomeMessageMapper));
        _publicationFinder.Find("some.routing.key").Returns(publication);

        var request = new SomeRequest();
        var message = new Message();
        var context = new AmanhecerContext { Request = request };
        context.SetMetadata("some.routing.key", MetadataName.PublicationRoutingKey);

        var mapper = Substitute.For<IMessageMapper>();
        mapper.ToMessageAsync(request, context).Returns(new ValueTask<Message>(message));
        _messageMapperFactory.Create(typeof(SomeMessageMapper)).Returns(mapper);

        var pipeline = Substitute.For<IEncodeTransformerPipeline>();
        _transformerPipelineFactory
            .Create("Amanhecer.Messaging.Transformer.Encode.resolved-publication", context)
            .Returns(pipeline);

        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        await _middleware.ExecuteAsync(context, next);

        _publicationFinder.Received(1).Find("some.routing.key");

        await Assert.That(context.Metadata.ContainsValue(publication)).IsTrue();
        await Assert.That(context.Request).IsEqualTo(message);

        await next.Received(1).Invoke(context);
    }

    [Test]
    public async Task When_NoRoutingKeyInMetadata_Should_DefaultToRequestTypeFullName()
    {
        var publication = Substitute.For<IPublication>();
        publication.Name.Returns("defaulted-publication");
        publication.MessageMapperType.Returns(typeof(SomeMessageMapper));
        _publicationFinder.Find(typeof(SomeRequest).FullName!).Returns(publication);

        var request = new SomeRequest();
        var message = new Message();
        var context = new AmanhecerContext { Request = request };

        var mapper = Substitute.For<IMessageMapper>();
        mapper.ToMessageAsync(request, context).Returns(new ValueTask<Message>(message));
        _messageMapperFactory.Create(typeof(SomeMessageMapper)).Returns(mapper);

        var pipeline = Substitute.For<IEncodeTransformerPipeline>();
        _transformerPipelineFactory
            .Create("Amanhecer.Messaging.Transformer.Encode.defaulted-publication", context)
            .Returns(pipeline);

        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        await _middleware.ExecuteAsync(context, next);

        _publicationFinder.Received(1).Find(typeof(SomeRequest).FullName!);

        await Assert.That(context.GetMetadata<string?>(MetadataName.PublicationRoutingKey))
            .IsEqualTo(typeof(SomeRequest).FullName);
        await Assert.That(context.Metadata.ContainsValue(publication)).IsTrue();
        await Assert.That(context.Request).IsEqualTo(message);

        await next.Received(1).Invoke(context);
    }

    private sealed class SomeRequest;

    private sealed class SomeMessageMapper : IMessageMapper
    {
        public ValueTask<Message> ToMessageAsync(object request, AmanhecerContext context)
        {
            return new ValueTask<Message>(new Message());
        }

        public ValueTask<object> ToRequestAsync(Message message, AmanhecerContext context)
        {
            return new ValueTask<object>(new object());
        }
    }

    private sealed class AnotherMessageMapper : IMessageMapper
    {
        public ValueTask<Message> ToMessageAsync(object request, AmanhecerContext context)
        {
            return new ValueTask<Message>(new Message());
        }

        public ValueTask<object> ToRequestAsync(Message message, AmanhecerContext context)
        {
            return new ValueTask<object>(new object());
        }
    }
}
