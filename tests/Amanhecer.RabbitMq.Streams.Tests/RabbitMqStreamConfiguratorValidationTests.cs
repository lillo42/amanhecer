using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging.Transformers;
using Amanhecer.RabbitMq.Streams.Configurations;
using NSubstitute;

namespace Amanhecer.RabbitMq.Streams.Tests;

/// <summary>
/// Broker-independent tests for the validation performed by the RabbitMQ Streams configurators:
/// missing required settings surface as <see cref="InvalidOperationException"/> when the
/// publication or subscription is added, and invalid arguments are rejected by guard clauses.
/// </summary>
public class RabbitMqStreamConfiguratorValidationTests
{
    [Test]
    public async Task When_Adding_A_Publication_Without_A_Routing_Key_Should_Throw()
    {
        var configurator = new RabbitMqStreamPublicationsConfigurator();

        await Assert
            .That(() => configurator.AddPublication(publication => publication.Stream("tests.stream")))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task When_Adding_A_Publication_Without_A_Stream_Should_Throw()
    {
        var configurator = new RabbitMqStreamPublicationsConfigurator();

        await Assert
            .That(() => configurator.AddPublication(publication => publication.RoutingKey("tests")))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task When_Adding_A_Fully_Configured_Publication_Should_Not_Throw()
    {
        var configurator = new RabbitMqStreamPublicationsConfigurator();

        await Assert
            .That(() =>
                configurator.AddPublication(publication =>
                    publication
                        .RoutingKey("tests")
                        .Stream("tests.stream")
                )
            )
            .ThrowsNothing();
    }

    [Test]
    public async Task When_Adding_A_Null_Built_Publication_Should_Throw()
    {
        var configurator = new RabbitMqStreamPublicationsConfigurator();

        await Assert
            .That(() => configurator.AddPublication((RabbitMqStreamPublication)null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task When_Adding_A_Subscription_Without_A_Routing_Key_Should_Throw()
    {
        var configurator = new RabbitMqStreamSubscriptionsConfigurator();

        await Assert
            .That(() => configurator.AddSubscription(subscription => subscription.Stream("tests.stream")))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task When_Adding_A_Subscription_Without_A_Stream_Should_Throw()
    {
        var configurator = new RabbitMqStreamSubscriptionsConfigurator();

        await Assert
            .That(() => configurator.AddSubscription(subscription => subscription.ToRoutingKey("tests")))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task When_Adding_A_Fully_Configured_Subscription_Should_Not_Throw()
    {
        var configurator = new RabbitMqStreamSubscriptionsConfigurator();

        await Assert
            .That(() =>
                configurator.AddSubscription(subscription =>
                    subscription
                        .ToRoutingKey("tests")
                        .Stream("tests.stream")
                )
            )
            .ThrowsNothing();
    }

    [Test]
    public async Task When_Adding_A_Null_Built_Subscription_Should_Throw()
    {
        var configurator = new RabbitMqStreamSubscriptionsConfigurator();

        await Assert
            .That(() => configurator.AddSubscription((RabbitMqStreamSubscription)null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task When_Setting_An_Empty_Publication_Name_Should_Throw()
    {
        var configurator = new RabbitMqStreamPublicationConfigurator();

        await Assert.That(() => configurator.Name("")).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Setting_An_Empty_Publication_Routing_Key_Should_Throw()
    {
        var configurator = new RabbitMqStreamPublicationConfigurator();

        await Assert.That(() => configurator.RoutingKey("")).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Setting_An_Empty_Publication_Stream_Should_Throw()
    {
        var configurator = new RabbitMqStreamPublicationConfigurator();

        await Assert.That(() => configurator.Stream("")).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Setting_A_Null_Publication_Encoding_Should_Throw()
    {
        var configurator = new RabbitMqStreamPublicationConfigurator();

        await Assert.That(() => configurator.Encoding(null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task When_Setting_A_Null_Publication_Configure_Should_Throw()
    {
        var configurator = new RabbitMqStreamPublicationConfigurator();

        await Assert.That(() => configurator.Configure(null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task When_Setting_An_Empty_Publication_Default_Content_Type_Should_Throw()
    {
        var configurator = new RabbitMqStreamPublicationConfigurator();

        await Assert.That(() => configurator.DefaultContentType("")).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Adding_A_Publication_Transformer_With_A_Decode_Only_Type_Should_Throw()
    {
        var configurator = new RabbitMqStreamPublicationConfigurator();

        await Assert
            .That(() => configurator.Transformer(typeof(TestDecodeTransformer)))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Publication_Transformer_With_Delegate_Should_Append_The_Anonymous_Encode_Transformer()
    {
        var cfg = new RabbitMqStreamPublicationConfigurator();
        cfg.RoutingKey("tests").Stream("tests.stream");

        Func<Message, AmanhecerContext, Func<Message, AmanhecerContext, ValueTask>, ValueTask> func =
            (message, context, next) => next(message, context);
        cfg.Transformer(func, order: 7);

        var publication = BuildPublication(cfg);

        var options = publication.Transformers.Single(x =>
            x.TransformerType == typeof(AnonymousEncodeTransformer));
        await Assert.That(options.Order).IsEqualTo(7);
        await Assert.That(options.Metadata).IsSameReferenceAs(func);
    }

    [Test]
    public async Task When_Building_A_Publication_Should_Carry_The_Configured_Values()
    {
        var cfg = new RabbitMqStreamPublicationConfigurator();
        cfg.Name("publication.name")
            .RoutingKey("tests")
            .Stream("tests.stream")
            .UserId("tests.user")
            .Encoding(Encoding.Unicode)
            .MessageMapper<TestMessageMapper>()
            .AdditionalCloudEvent("tenant", "acme");

        var publication = BuildPublication(cfg);

        await Assert.That(publication.Name).IsEqualTo("publication.name");
        await Assert.That(publication.RoutingKey).IsEqualTo("tests");
        await Assert.That(publication.Stream).IsEqualTo("tests.stream");
        await Assert.That(publication.UserId).IsEqualTo("tests.user");
        await Assert.That(publication.Encoding).IsSameReferenceAs(Encoding.Unicode);
        await Assert.That(publication.MessageMapperType).IsEqualTo(typeof(TestMessageMapper));
        await Assert.That(publication.AdditionalCloudEvents["tenant"]).IsEqualTo("acme");
    }

    [Test]
    public async Task When_Building_A_Publication_Without_A_Name_Should_Generate_One()
    {
        var cfg = new RabbitMqStreamPublicationConfigurator();
        cfg.RoutingKey("tests").Stream("tests.stream");

        var publication = BuildPublication(cfg);

        await Assert.That(publication.Name).IsNotNull();
        await Assert.That(publication.Name).IsNotEqualTo(string.Empty);
    }

    [Test]
    public async Task When_Setting_An_Empty_Subscription_Name_Should_Throw()
    {
        var configurator = new RabbitMqStreamSubscriptionConfigurator();

        await Assert.That(() => configurator.Name("")).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Setting_An_Empty_Subscription_To_Routing_Key_Should_Throw()
    {
        var configurator = new RabbitMqStreamSubscriptionConfigurator();

        await Assert.That(() => configurator.ToRoutingKey("")).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Setting_An_Empty_Subscription_Stream_Should_Throw()
    {
        var configurator = new RabbitMqStreamSubscriptionConfigurator();

        await Assert.That(() => configurator.Stream("")).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Setting_A_Negative_Buffer_Size_Should_Throw()
    {
        var configurator = new RabbitMqStreamSubscriptionConfigurator();

        await Assert
            .That(() => configurator.BufferSize(-1))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task When_Setting_A_Zero_Buffer_Size_Should_Throw()
    {
        var configurator = new RabbitMqStreamSubscriptionConfigurator();

        await Assert
            .That(() => configurator.BufferSize(0))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task When_Setting_A_Negative_Number_Of_Consumers_Should_Throw()
    {
        var configurator = new RabbitMqStreamSubscriptionConfigurator();

        await Assert
            .That(() => configurator.NumberOfConsumers(-1))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task When_Setting_An_Empty_Subscription_Spec_Version_Should_Throw()
    {
        var configurator = new RabbitMqStreamSubscriptionConfigurator();

        await Assert.That(() => configurator.DefaultSpecVersion("")).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Setting_An_Empty_Dead_Letter_Queue_Routing_Key_Should_Throw()
    {
        var configurator = new RabbitMqStreamSubscriptionConfigurator();

        await Assert
            .That(() => configurator.DeadLetterQueueRoutingKey(""))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Setting_An_Empty_Invalid_Message_Routing_Key_Should_Throw()
    {
        var configurator = new RabbitMqStreamSubscriptionConfigurator();

        await Assert
            .That(() => configurator.InvalidMessageRoutingKey(""))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Setting_A_Null_On_Error_Should_Throw()
    {
        var configurator = new RabbitMqStreamSubscriptionConfigurator();

        await Assert.That(() => configurator.OnError(null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task When_Subscription_Transformer_With_An_Encode_Only_Type_Should_Throw()
    {
        var configurator = new RabbitMqStreamSubscriptionConfigurator();

        await Assert
            .That(() => configurator.Transformer(typeof(TestEncodeTransformer)))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_Subscription_Transformer_With_Delegate_Should_Append_The_Anonymous_Decode_Transformer()
    {
        var cfg = new RabbitMqStreamSubscriptionConfigurator();
        cfg.ToRoutingKey("tests").Stream("tests.stream");

        Func<Message, AmanhecerContext, Func<Message, AmanhecerContext, ValueTask>, ValueTask> func =
            (message, context, next) => next(message, context);
        cfg.Transformer(func, order: 6);

        var subscription = BuildSubscription(cfg);

        var options = subscription.Transformers.Single(x =>
            x.TransformerType == typeof(AnonymousDecodeTransformer));
        await Assert.That(options.Order).IsEqualTo(6);
        await Assert.That(options.Metadata).IsSameReferenceAs(func);
    }

    [Test]
    public async Task When_Building_A_Subscription_Should_Carry_The_Configured_Values()
    {
        var cfg = new RabbitMqStreamSubscriptionConfigurator();
        cfg.Name("subscription.name")
            .ToRoutingKey("tests")
            .Stream("tests.stream")
            .NumberOfConsumers(3)
            .BufferSize(42)
            .JsonCloudEvent()
            .MessageMapper<TestMessageMapper>()
            .DeadLetterQueueRoutingKey("dead.letter")
            .InvalidMessageRoutingKey("invalid");

        var subscription = BuildSubscription(cfg);

        await Assert.That(subscription.Name).IsEqualTo("subscription.name");
        await Assert.That(subscription.ToRoutingKey).IsEqualTo("tests");
        await Assert.That(subscription.Stream).IsEqualTo("tests.stream");
        await Assert.That(subscription.NumberOfConsumers).IsEqualTo(3);
        await Assert.That(subscription.BufferSize).IsEqualTo(42);
        await Assert.That(subscription.CloudEventType).IsEqualTo(CloudEventType.Json);
        await Assert.That(subscription.MessageMapperType).IsEqualTo(typeof(TestMessageMapper));
        await Assert.That(subscription.DeadLetterQueueRoutingKey).IsEqualTo("dead.letter");
        await Assert.That(subscription.InvalidMessageRoutingKey).IsEqualTo("invalid");
    }

    [Test]
    public async Task When_A_Subscription_Sets_On_Error_Should_Carry_The_Handler()
    {
        var cfg = new RabbitMqStreamSubscriptionConfigurator();
        cfg.ToRoutingKey("tests").Stream("tests.stream");

        Func<Message, Exception, IConsumerAction> onError = (_, _) => Substitute.For<IConsumerAction>();
        cfg.OnError(onError);

        var subscription = BuildSubscription(cfg);

        await Assert.That(subscription.OnError).IsSameReferenceAs(onError);
    }

    [Test]
    public async Task When_The_Default_Message_Mapper_Is_Set_Should_Apply_To_Publications_And_Subscriptions()
    {
        var configurator = new RabbitMqStreamConfigurator();
        configurator
            .Publications(publications =>
                publications.AddPublication(publication =>
                    publication.RoutingKey("tests").Stream("tests.stream")))
            .Subscriptions(subscriptions =>
                subscriptions.AddSubscription(subscription =>
                    subscription.ToRoutingKey("tests").Stream("tests.stream")))
            .DefaultMessageMapper<TestMessageMapper>();

        var gateway = (RabbitMqStreamGateway)CreateGateway(configurator);

        await Assert.That(gateway.Publications.Single().MessageMapperType).IsEqualTo(typeof(TestMessageMapper));
        await Assert.That(gateway.Subscriptions.Single().MessageMapperType).IsEqualTo(typeof(TestMessageMapper));
    }

    [Test]
    public async Task When_A_Publication_Sets_Its_Own_Message_Mapper_Should_Not_Be_Overridden_By_The_Default()
    {
        var configurator = new RabbitMqStreamConfigurator();
        configurator
            .Publications(publications =>
                publications.AddPublication(publication =>
                    publication
                        .RoutingKey("tests")
                        .Stream("tests.stream")
                        .MessageMapper<TestMessageMapper>()))
            .DefaultMessageMapper<OtherMessageMapper>();

        var gateway = (RabbitMqStreamGateway)CreateGateway(configurator);

        await Assert.That(gateway.Publications.Single().MessageMapperType).IsEqualTo(typeof(TestMessageMapper));
    }

    [Test]
    public async Task When_The_Default_Message_Mapper_Does_Not_Implement_IMessageMapper_Should_Throw()
    {
        var configurator = new RabbitMqStreamConfigurator();

        await Assert
            .That(() => configurator.DefaultMessageMapper(typeof(object)))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_The_Connection_Is_Configured_Should_Apply_It_To_The_Gateway()
    {
        var configurator = new RabbitMqStreamConfigurator();
        configurator.Connection(connection =>
            connection
                .Credentials("tester", "secret")
                .VirtualHost("/tests")
                .UserId("tests.user")
                .Endpoint("broker.local", 5600));

        var gateway = (RabbitMqStreamGateway)CreateGateway(configurator);

        await Assert.That(gateway.UserName).IsEqualTo("tester");
        await Assert.That(gateway.Password).IsEqualTo("secret");
        await Assert.That(gateway.VirtualHost).IsEqualTo("/tests");
        await Assert.That(gateway.UserId).IsEqualTo("tests.user");
        await Assert.That(gateway.EndPoints.Count).IsEqualTo(1);
    }

    private static RabbitMqStreamPublication BuildPublication(RabbitMqStreamPublicationConfigurator cfg)
    {
        var toPublication = typeof(RabbitMqStreamPublicationConfigurator).GetMethod(
            "ToPublication",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        return (RabbitMqStreamPublication)toPublication.Invoke(cfg, null)!;
    }

    private static RabbitMqStreamSubscription BuildSubscription(RabbitMqStreamSubscriptionConfigurator cfg)
    {
        var toSubscription = typeof(RabbitMqStreamSubscriptionConfigurator).GetMethod(
            "ToSubscription",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        return (RabbitMqStreamSubscription)toSubscription.Invoke(cfg, null)!;
    }

    private static IGateway CreateGateway(RabbitMqStreamConfigurator cfg)
    {
        var createGateway = typeof(RabbitMqStreamConfigurator).GetMethod(
            "CreateGateway",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        return (IGateway)createGateway.Invoke(cfg, null)!;
    }

    private sealed class TestEncodeTransformer : IEncodeTransformer
    {
        public ValueTask EncodeAsync(
            Message message,
            AmanhecerContext context,
            Func<Message, AmanhecerContext, ValueTask> next)
        {
            return next(message, context);
        }
    }

    private sealed class TestDecodeTransformer : IDecodeTransformer
    {
        public ValueTask DecodeAsync(
            Message message,
            AmanhecerContext context,
            Func<Message, AmanhecerContext, ValueTask> next)
        {
            return next(message, context);
        }
    }

    private class TestMessageMapper : IMessageMapper
    {
        public ValueTask<Message> ToMessageAsync(object request, AmanhecerContext context)
            => throw new NotImplementedException();

        public ValueTask<object> ToRequestAsync(Message message, AmanhecerContext context)
            => throw new NotImplementedException();
    }

    private sealed class OtherMessageMapper : TestMessageMapper;
}
