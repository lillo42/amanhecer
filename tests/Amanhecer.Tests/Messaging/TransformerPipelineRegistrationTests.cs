using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Configurator;
using Amanhecer.Extensions;
using Amanhecer.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Amanhecer.Tests.Messaging;

public class TransformerPipelineRegistrationTests
{
    [Test]
    public async Task When_PublicationHasTransformers_Should_RegisterEncodePipeline()
    {
        var publication = new TestPublication
        {
            Name = "orders-pub",
            RoutingKey = "orders",
            Transformers = [new AmanhecerTransformerOptions(typeof(EncodeOnlyTransformer), 3, null)]
        };
        var services = RegisterGateway(publications: [publication]);

        var options = GetOptions(services);

        var key = "Amanhecer.Messaging.Transformer.Encode.orders-pub";
        await Assert.That(options.Configuration.ContainsKey(key)).IsTrue();
        await Assert.That(options.Configuration[key].Single().TransformerType)
            .IsEqualTo(typeof(EncodeOnlyTransformer));
    }

    [Test]
    public async Task When_PublicationHasTransformers_Should_RegisterTransformerTypeInServices()
    {
        var publication = new TestPublication
        {
            Name = "orders-pub",
            RoutingKey = "orders",
            Transformers = [new AmanhecerTransformerOptions(typeof(EncodeOnlyTransformer), 3, null)]
        };
        var services = RegisterGateway(publications: [publication]);

        await Assert.That(services.Any(x => x.ServiceType == typeof(EncodeOnlyTransformer))).IsTrue();
    }

    [Test]
    public async Task When_MapperHasTransformerAttribute_Should_AddTransformerToPublicationEncodePipeline()
    {
        var publication = new TestPublication
        {
            Name = "orders-pub",
            RoutingKey = "orders",
            MessageMapperType = typeof(AttributedMapper)
        };
        var services = RegisterGateway(publications: [publication]);

        var options = GetOptions(services);

        var key = "Amanhecer.Messaging.Transformer.Encode.orders-pub";
        await Assert.That(options.Configuration.ContainsKey(key)).IsTrue();

        var transformer = options.Configuration[key].Single();
        await Assert.That(transformer.TransformerType).IsEqualTo(typeof(BothWaysTransformer));
        await Assert.That(transformer.Order).IsEqualTo(5);
        await Assert.That(transformer.Metadata).IsTypeOf<TestTransformerAttribute>();
    }

    [Test]
    public async Task When_MapperHasTransformerAttribute_Should_AddTransformerToSubscriptionDecodePipeline()
    {
        var subscription = Substitute.For<ISubscription>();
        subscription.Name.Returns("orders-sub");
        subscription.MessageMapperType.Returns(typeof(AttributedMapper));

        var services = RegisterGateway(subscriptions: [subscription]);

        var options = GetOptions(services);

        var key = "Amanhecer.Messaging.Transformer.Decode.orders-sub";
        await Assert.That(options.Configuration.ContainsKey(key)).IsTrue();
        await Assert.That(options.Configuration[key].Single().TransformerType)
            .IsEqualTo(typeof(BothWaysTransformer));
    }

    [Test]
    public async Task When_PublicationHasTransformersAndMapperAttributes_Should_MergeOrderedByOrder()
    {
        var publication = new TestPublication
        {
            Name = "orders-pub",
            RoutingKey = "orders",
            MessageMapperType = typeof(AttributedMapper), // order 5
            Transformers = [new AmanhecerTransformerOptions(typeof(EncodeOnlyTransformer), 10, null)]
        };
        var services = RegisterGateway(publications: [publication]);

        var options = GetOptions(services);

        var transformers = options.Configuration["Amanhecer.Messaging.Transformer.Encode.orders-pub"];
        await Assert.That(transformers.Count).IsEqualTo(2);
        await Assert.That(transformers[0].TransformerType).IsEqualTo(typeof(BothWaysTransformer));
        await Assert.That(transformers[1].TransformerType).IsEqualTo(typeof(EncodeOnlyTransformer));
    }

    [Test]
    public async Task When_GlobalTransformerRegistered_Should_BeAddedToEveryPipeline()
    {
        var publication = new TestPublication
        {
            Name = "orders-pub",
            RoutingKey = "orders",
            Transformers = [new AmanhecerTransformerOptions(typeof(EncodeOnlyTransformer), 10, null)]
        };
        var subscription = Substitute.For<ISubscription>();
        subscription.Name.Returns("orders-sub");
        subscription.MessageMapperType.Returns(typeof(AttributedMapper));

        var services = RegisterGateway(
            publications: [publication],
            subscriptions: [subscription],
            configure: m => m.AddGlobalTransformer<BothWaysTransformer>(order: 2));

        var options = GetOptions(services);

        var encode = options.Configuration["Amanhecer.Messaging.Transformer.Encode.orders-pub"];
        await Assert.That(encode.Count).IsEqualTo(2);
        await Assert.That(encode[0].TransformerType).IsEqualTo(typeof(BothWaysTransformer));
        await Assert.That(encode[0].Order).IsEqualTo(2);
        await Assert.That(encode[1].TransformerType).IsEqualTo(typeof(EncodeOnlyTransformer));

        var decode = options.Configuration["Amanhecer.Messaging.Transformer.Decode.orders-sub"];
        await Assert.That(decode.Count).IsEqualTo(2);
        await Assert.That(decode[0].TransformerType).IsEqualTo(typeof(BothWaysTransformer));
        await Assert.That(decode[0].Order).IsEqualTo(2);
        await Assert.That(decode[1].Order).IsEqualTo(5);

        await Assert.That(services.Any(x => x.ServiceType == typeof(BothWaysTransformer))).IsTrue();
    }

    [Test]
    public async Task When_GlobalTransformerRegistered_Should_BeAddedToExplicitlyNamedPipelines()
    {
        var services = RegisterGateway(configure: m => m
            .AddGlobalTransformer<BothWaysTransformer>(order: 0)
            .AddTransformerPipeline("custom",
                [new AmanhecerTransformerOptions(typeof(EncodeOnlyTransformer), 10, null)]));

        var options = GetOptions(services);

        var custom = options.Configuration["custom"];
        await Assert.That(custom.Count).IsEqualTo(2);
        await Assert.That(custom[0].TransformerType).IsEqualTo(typeof(BothWaysTransformer));
        await Assert.That(custom[1].TransformerType).IsEqualTo(typeof(EncodeOnlyTransformer));
    }

    [Test]
    public async Task When_GlobalTransformerImplementsNeitherInterface_Should_Throw()
    {
        var services = new ServiceCollection();

        await Assert.That(() => services.AddAmanhecer(cfg => cfg
                .UsingMessagingGateway(m => m.AddGlobalTransformer(typeof(string)))))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task When_MapperAttributePointsToNonTransformer_Should_Throw()
    {
        var publication = new TestPublication
        {
            Name = "orders-pub",
            RoutingKey = "orders",
            MessageMapperType = typeof(InvalidAttributedMapper)
        };

        await Assert.That(() => RegisterGateway(publications: [publication]))
            .Throws<InvalidOperationException>();
    }

    private static ServiceCollection RegisterGateway(
        IReadOnlyList<IPublication>? publications = null,
        IReadOnlyList<ISubscription>? subscriptions = null,
        Action<AmanhecerMessagingConfigurator>? configure = null)
    {
        var gateway = Substitute.For<IGateway>();
        gateway.Name.Returns("test-gateway");
        gateway.Publications.Returns(publications ?? []);
        gateway.Subscriptions.Returns(subscriptions ?? []);
        gateway.CreateProducers().Returns(new Dictionary<string, IProducer>());

        var services = new ServiceCollection();
        services.AddAmanhecer(cfg => cfg.UsingMessagingGateway(m =>
        {
            configure?.Invoke(m);
            m.AddGateway(gateway);
        }));
        return services;
    }

    private static AmanhecerTransformerPipelineOptions GetOptions(IServiceCollection services)
    {
        return services.BuildServiceProvider().GetRequiredService<AmanhecerTransformerPipelineOptions>();
    }
}
