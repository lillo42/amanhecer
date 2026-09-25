using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Configurator;
using Amanhecer.Extensions;
using Amanhecer.Messaging.Transformers;
using Amanhecer.RabbitMq.Configurations;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using RabbitMQ.Client;

namespace Amanhecer.RabbitMq.Tests;

/// <summary>
/// Broker-independent tests for the CloudEvents structured (JSON) content mode: the envelope
/// is produced and consumed by the <see cref="StructuredCloudEventTransformer"/> the
/// configurators register into the pipeline, and the transport only omits the
/// <c>cloudEvents:*</c> headers.
/// </summary>
public class RabbitMqStructuredCloudEventsTests
{
    [Test]
    public async Task When_Producing_In_Structured_Mode_Should_Set_CloudEvents_Headers()
    {
        var (producer, publishes) = CreateProducer();
        var publication = CreatePublication();
        publication.CloudEventType = CloudEventType.Json;
        var message = new Message
        {
            ContentType = new ContentType(StructuredCloudEventTransformer.MediaType),
            Source = new Uri("amanhecer.tests", UriKind.RelativeOrAbsolute),
            SpecVersion = "1.0",
            Type = "amanhecer.tests.message",
            Payload = """{"message":"test"}"""u8.ToArray()
        };

        await producer.ProduceAsync(message, publication, new AmanhecerContext());

        var headers = publishes[0].Properties.Headers;
        var cloudEventsHeaders = headers?.Keys.Where(key => key.StartsWith("cloudEvents:")).ToArray() ?? [];
        await Assert.That(cloudEventsHeaders).Contains("cloudEvents:id");
        await Assert.That(cloudEventsHeaders).Contains("cloudEvents:source");
        await Assert.That(cloudEventsHeaders).Contains("cloudEvents:specversion");
        await Assert.That(cloudEventsHeaders).Contains("cloudEvents:type");
    }

    [Test]
    public async Task When_The_Publication_Uses_Structured_CloudEvents_Should_Register_The_Encode_Transformer()
    {
        var services = RegisterGateway(publications => publications.AddPublication(publication => publication
                .Name("structured-pub")
                .RoutingKey("tests")
                .RabbitMqRoutingKey("tests")
                .CloudEventType(CloudEventType.Json)
                .Exchange(new Exchange { Name = "tests.exchange" })),
            null);

        var options = GetOptions(services);

        var transformers = options.Configuration["Amanhecer.Messaging.Transformer.Encode.structured-pub"];
        await Assert.That(transformers.Count).IsEqualTo(1);
        await Assert.That(transformers[0].TransformerType).IsEqualTo(typeof(StructuredCloudEventTransformer));
        await Assert.That(services.Any(x => x.ServiceType == typeof(StructuredCloudEventTransformer))).IsTrue();
    }

    [Test]
    public async Task When_The_Publication_Uses_Binary_CloudEvents_Should_Not_Register_The_Encode_Transformer()
    {
        var services = RegisterGateway(publications => publications.AddPublication(publication => publication
                .Name("binary-pub")
                .RoutingKey("tests")
                .RabbitMqRoutingKey("tests")
                .Exchange(new Exchange { Name = "tests.exchange" })),
            null);

        var options = GetOptions(services);

        await Assert.That(options.Configuration.ContainsKey("Amanhecer.Messaging.Transformer.Encode.binary-pub"))
            .IsFalse();
    }

    [Test]
    public async Task When_Adding_A_Subscription_Should_Register_The_Decode_Transformer_First()
    {
        var services = RegisterGateway(null,
            subscriptions => subscriptions.AddSubscription(subscription => subscription
                .Name("structured-sub")
                .ToRoutingKey("tests")
                .QueueName("tests.queue")));

        var options = GetOptions(services);

        var transformers = options.Configuration["Amanhecer.Messaging.Transformer.Decode.structured-sub"];
        await Assert.That(transformers[0].TransformerType).IsEqualTo(typeof(StructuredCloudEventTransformer));
    }

    [Test]
    public async Task When_Configuring_A_Subscription_With_CloudEvent_Should_Set_The_CloudEvent_Type()
    {
        var subscription = CreateSubscription(subscription => subscription
            .Name("configured-sub")
            .ToRoutingKey("tests")
            .QueueName("tests.queue")
            .CloudEvent(CloudEventType.Json));

        await Assert.That(subscription.CloudEventType).IsEqualTo(CloudEventType.Json);
    }

    [Test]
    public async Task When_Configuring_A_Subscription_With_JsonCloudEvent_Should_Set_The_CloudEvent_Type_To_Json()
    {
        var subscription = CreateSubscription(subscription => subscription
            .Name("json-sub")
            .ToRoutingKey("tests")
            .QueueName("tests.queue")
            .JsonCloudEvent());

        await Assert.That(subscription.CloudEventType).IsEqualTo(CloudEventType.Json);
    }

    [Test]
    public async Task When_Configuring_A_Subscription_With_BinaryCloudEvent_Should_Set_The_CloudEvent_Type_To_Binary()
    {
        var subscription = CreateSubscription(subscription => subscription
            .Name("binary-sub")
            .ToRoutingKey("tests")
            .QueueName("tests.queue")
            .JsonCloudEvent()
            .BinaryCloudEvent());

        await Assert.That(subscription.CloudEventType).IsEqualTo(CloudEventType.Binary);
    }

    private static ServiceCollection RegisterGateway(
        Action<RabbitMqPublicationsConfigurator>? publications,
        Action<RabbitMqSubscriptionsConfigurator>? subscriptions)
    {
        var services = new ServiceCollection();
        services.AddAmanhecer(cfg => cfg.UsingMessagingGateway(m => m.UsingRabbitMQ(rabbitMq =>
        {
            if (publications != null)
            {
                rabbitMq.Publications(publications);
            }

            if (subscriptions != null)
            {
                rabbitMq.Subscriptions(subscriptions);
            }
        })));
        return services;
    }

    private static AmanhecerTransformerPipelineOptions GetOptions(IServiceCollection services)
    {
        return (AmanhecerTransformerPipelineOptions)services
            .Single(x => x.ServiceType == typeof(AmanhecerTransformerPipelineOptions))
            .ImplementationInstance!;
    }

    private static ISubscription CreateSubscription(Action<RabbitMqSubscriptionConfigurator> configure)
    {
        var cfg = new RabbitMqSubscriptionConfigurator();
        configure.Invoke(cfg);

        var toSubscription = typeof(RabbitMqSubscriptionConfigurator)
            .GetMethod("ToSubscription", BindingFlags.Instance | BindingFlags.NonPublic)!;

        return (ISubscription)toSubscription.Invoke(cfg, null)!;
    }

    private static (RabbitMqProducer Producer, List<Publish> Publishes) CreateProducer()
    {
        var channel = Substitute.For<IChannel>();
        var publishes = new List<Publish>();

        channel
            .BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                publishes.Add(new Publish(callInfo.ArgAt<BasicProperties>(3)));
                return ValueTask.CompletedTask;
            });

        return (new RabbitMqProducer(channel), publishes);
    }

    private static RabbitMqPublication CreatePublication()
    {
        return new RabbitMqPublication
        {
            RoutingKey = "tests",
            RabbitMqRoutingKey = "tests.rabbitmq",
            Exchange = new Exchange { Name = "tests.exchange" }
        };
    }

    private sealed record Publish(BasicProperties Properties);
}
