using System;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging.Middlewares;
using NSubstitute;

namespace Amanhecer.Tests.Messaging;

public class DecodeMiddlewareTests
{
    private readonly IDecodeTransformerPipelineFactory _transformerPipelineFactory;
    private readonly IMessageMapperFactory _messageMapperFactory;
    private readonly DecodeMiddleware _middleware;

    public DecodeMiddlewareTests()
    {
        _transformerPipelineFactory = Substitute.For<IDecodeTransformerPipelineFactory>();
        _messageMapperFactory = Substitute.For<IMessageMapperFactory>();
        _middleware = new DecodeMiddleware(_transformerPipelineFactory, _messageMapperFactory);
    }

    [Test]
    public async Task When_RequestIsNotAMessage_Should_PassThroughUnchanged()
    {
        var request = new SomeRequest();
        var context = new AmanhecerContext { Request = request };
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        await _middleware.ExecuteAsync(context, next);

        await next.Received(1).Invoke(context);
        await Assert.That(context.Request).IsEqualTo(request);
        _transformerPipelineFactory.DidNotReceive().Create(Arg.Any<string>(), Arg.Any<AmanhecerContext>());
        _messageMapperFactory.DidNotReceive().Create(Arg.Any<Type>());
    }

    [Test]
    public async Task When_NoSubscriptionInMetadata_Should_PassThroughUnchanged()
    {
        var message = new Message();
        var context = new AmanhecerContext { Request = message };
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        await _middleware.ExecuteAsync(context, next);

        await next.Received(1).Invoke(context);
        await Assert.That(context.Request).IsEqualTo(message);
        _transformerPipelineFactory.DidNotReceive().Create(Arg.Any<string>(), Arg.Any<AmanhecerContext>());
        _messageMapperFactory.DidNotReceive().Create(Arg.Any<Type>());
    }

    [Test]
    public async Task When_NoMessageMapperTypeInMetadata_Should_ThrowInvalidOperationException()
    {
        var subscription = new SomeSubscription();
        var context = new AmanhecerContext { Request = new Message() };
        context.SetMetadata(subscription, MetadataName.Subscription);
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        await Assert.That(async () => await _middleware.ExecuteAsync(context, next))
            .Throws<InvalidOperationException>();

        await next.DidNotReceive().Invoke(context);
        _transformerPipelineFactory.DidNotReceive().Create(Arg.Any<string>(), Arg.Any<AmanhecerContext>());
        _messageMapperFactory.DidNotReceive().Create(Arg.Any<Type>());
    }

    [Test]
    public async Task When_MessageAndSubscription_Should_DecodeAndMapIntoRequest()
    {
        var subscription = new SomeSubscription { Name = "some-subscription" };
        var message = new Message();
        var mappedRequest = new SomeRequest();
        var context = new AmanhecerContext { Request = message };
        context.SetMetadata(subscription, MetadataName.Subscription);
        context.SetMetadata(typeof(SomeMessageMapper), MetadataName.MessageMapperType);

        var pipeline = Substitute.For<IDecodeTransformerPipeline>();
        _transformerPipelineFactory
            .Create("Amanhecer.Messaging.Transformer.Decode.some-subscription", context)
            .Returns(pipeline);

        var mapper = Substitute.For<IMessageMapper>();
        mapper.ToRequestAsync(message, context).Returns(new ValueTask<object>(mappedRequest));
        _messageMapperFactory.Create(typeof(SomeMessageMapper)).Returns(mapper);

        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        await _middleware.ExecuteAsync(context, next);

        _transformerPipelineFactory
            .Received(1)
            .Create("Amanhecer.Messaging.Transformer.Decode.some-subscription", context);
        await pipeline.Received(1).DecodeAsync(message, context);
        _messageMapperFactory.Received(1).Create(typeof(SomeMessageMapper));
        await mapper.Received(1).ToRequestAsync(message, context);

        await Assert.That(context.Request).IsEqualTo(mappedRequest);
        await Assert.That(context.GetMetadata<Message>()).IsEqualTo(message);

        await next.Received(1).Invoke(context);
    }

    private sealed class SomeRequest;

    private sealed class SomeSubscription() : Subscription("some.routing.key");

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
}
