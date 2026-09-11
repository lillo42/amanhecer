using System;
using System.Linq;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Configurator;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Amanhecer.Tests.Configurator;

public class AmanhecerMessagingConfiguratorTests
{
    [Test]
    public async Task When_Created_Should_ExposeServicesAndEmptyCollections()
    {
        var services = new ServiceCollection();

        var configurator = new AmanhecerMessagingConfigurator(services);

        await Assert.That(configurator.Services).IsSameReferenceAs(services);
        await Assert.That(configurator.Gateways).Count().IsEqualTo(0);
        await Assert.That(configurator.TransformerPipeline).Count().IsEqualTo(0);
        await Assert.That(configurator.GlobalTransformers).Count().IsEqualTo(0);
    }

    [Test]
    public async Task When_DefaultMessageMapper_WithNonMapperType_Should_ThrowArgumentException()
    {
        var configurator = new AmanhecerMessagingConfigurator(new ServiceCollection());

        await Assert.That(() => configurator.DefaultMessageMapper(typeof(string)))
            .ThrowsExactly<ArgumentException>()
            .WithParameterName("messageMapperType");
    }

    [Test]
    public async Task When_DefaultMessageMapper_Should_RegisterMapperAsTransient()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhecerMessagingConfigurator(services);

        var result = configurator.DefaultMessageMapper(typeof(TestMessageMapper));

        await Assert.That(result).IsSameReferenceAs(configurator);
        await Assert.That(services)
            .Contains(x => x.ServiceType == typeof(TestMessageMapper)
                && x.ImplementationType == typeof(TestMessageMapper)
                && x.Lifetime == ServiceLifetime.Transient);
    }

    [Test]
    public async Task When_DefaultMessageMapperGeneric_Should_RegisterMapperAsTransient()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhecerMessagingConfigurator(services);

        var result = configurator.DefaultMessageMapper<TestMessageMapper>();

        await Assert.That(result).IsSameReferenceAs(configurator);
        await Assert.That(services)
            .Contains(x => x.ServiceType == typeof(TestMessageMapper)
                && x.Lifetime == ServiceLifetime.Transient);
    }

    [Test]
    public async Task When_DefaultMessageMapper_CalledTwiceWithSameType_Should_RegisterTypeOnce()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhecerMessagingConfigurator(services);

        configurator.DefaultMessageMapper<TestMessageMapper>();
        configurator.DefaultMessageMapper<TestMessageMapper>();

        await Assert.That(services.Count(x => x.ServiceType == typeof(TestMessageMapper)))
            .IsEqualTo(1);
    }

    [Test]
    public async Task When_AddTransformerPipeline_Should_OrderTransformersByOrder()
    {
        var configurator = new AmanhecerMessagingConfigurator(new ServiceCollection());

        var result = configurator.AddTransformerPipeline("pipeline",
        [
            new AmanhecerTransformerOptions(typeof(string), 10, null),
            new AmanhecerTransformerOptions(typeof(int), -5, "meta"),
            new AmanhecerTransformerOptions(typeof(double), 0, null)
        ]);

        await Assert.That(result).IsSameReferenceAs(configurator);

        var pipeline = configurator.TransformerPipeline["pipeline"];
        await Assert.That(pipeline).Count().IsEqualTo(3);
        await Assert.That(pipeline[0].TransformerType).IsEqualTo(typeof(int));
        await Assert.That(pipeline[0].Order).IsEqualTo(-5);
        await Assert.That(pipeline[0].Metadata).IsEqualTo("meta");
        await Assert.That(pipeline[1].TransformerType).IsEqualTo(typeof(double));
        await Assert.That(pipeline[2].TransformerType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task When_AddTransformerPipeline_WithDuplicateName_Should_ThrowInvalidOperationException()
    {
        var configurator = new AmanhecerMessagingConfigurator(new ServiceCollection());
        configurator.AddTransformerPipeline("pipeline",
            [new AmanhecerTransformerOptions(typeof(string), 0, null)]);

        await Assert.That(() => configurator.AddTransformerPipeline("pipeline",
                [new AmanhecerTransformerOptions(typeof(int), 0, null)]))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessageContaining("pipeline");
    }

    [Test]
    public async Task When_AddGlobalTransformer_Should_AddOptionsAndRegisterType()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhecerMessagingConfigurator(services);
        var metadata = new object();

        var result = configurator.AddGlobalTransformer<TestEncodeTransformer>(order: 3, metadata: metadata);

        await Assert.That(result).IsSameReferenceAs(configurator);

        var options = configurator.GlobalTransformers.Single();
        await Assert.That(options.TransformerType).IsEqualTo(typeof(TestEncodeTransformer));
        await Assert.That(options.Order).IsEqualTo(3);
        await Assert.That(options.Metadata).IsSameReferenceAs(metadata);

        await Assert.That(services)
            .Contains(x => x.ServiceType == typeof(TestEncodeTransformer)
                && x.Lifetime == ServiceLifetime.Transient);
    }

    [Test]
    public async Task When_AddGlobalTransformer_Should_UseDefaultOrderAndNullMetadata()
    {
        var configurator = new AmanhecerMessagingConfigurator(new ServiceCollection());

        configurator.AddGlobalTransformer(typeof(TestDecodeTransformer));

        var options = configurator.GlobalTransformers.Single();
        await Assert.That(options.TransformerType).IsEqualTo(typeof(TestDecodeTransformer));
        await Assert.That(options.Order).IsEqualTo(0);
        await Assert.That(options.Metadata).IsNull();
    }

    [Test]
    public async Task When_AddGlobalTransformer_WithNeitherInterface_Should_ThrowArgumentException()
    {
        var configurator = new AmanhecerMessagingConfigurator(new ServiceCollection());

        await Assert.That(() => configurator.AddGlobalTransformer(typeof(string)))
            .ThrowsExactly<ArgumentException>()
            .WithParameterName("transformerType");
    }

    [Test]
    public async Task When_AddGateway_Should_RegisterGatewayAsSingletonWithoutProvisioning()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhecerMessagingConfigurator(services);
        var gateway = CreateGateway();

        var result = configurator.AddGateway(gateway);

        await Assert.That(result).IsSameReferenceAs(configurator);
        await gateway.DidNotReceive().ProvisionerAsync();
        await Assert.That(configurator.Gateways).Count().IsEqualTo(1);
        await Assert.That(configurator.Gateways[0]).IsSameReferenceAs(gateway);
        await Assert.That(services)
            .Contains(x => x.ServiceType == typeof(IGateway)
                && x.Lifetime == ServiceLifetime.Singleton);

        var provider = services.BuildServiceProvider();
        await Assert.That(provider.GetRequiredService<IGateway>()).IsSameReferenceAs(gateway);
    }

    [Test]
    public async Task When_TheContainerIsDisposed_Should_DisposeGatewaysAddedViaAddGateway()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhecerMessagingConfigurator(services);
        var gateway = Substitute.For<IGateway, IDisposable>();
        gateway.Publications.Returns([]);
        gateway.Subscriptions.Returns([]);

        configurator.AddGateway(gateway);

        var disposed = false;
        ((IDisposable)gateway).When(x => x.Dispose()).Do(_ => disposed = true);

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IGateway>();
        provider.Dispose();

        await Assert.That(disposed).IsTrue();
    }

    [Test]
    public async Task When_AddGateway_Should_RegisterConfiguredMessageMapperTypes()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhecerMessagingConfigurator(services);

        var publication = Substitute.For<IPublication>();
        publication.MessageMapperType.Returns(typeof(TestMessageMapper));
        var subscription = Substitute.For<ISubscription>();
        subscription.MessageMapperType.Returns(typeof(AnotherMessageMapper));

        configurator.AddGateway(CreateGateway(publications: [publication], subscriptions: [subscription]));

        await Assert.That(services)
            .Contains(x => x.ServiceType == typeof(TestMessageMapper)
                && x.Lifetime == ServiceLifetime.Transient)
            .And.Contains(x => x.ServiceType == typeof(AnotherMessageMapper)
                && x.Lifetime == ServiceLifetime.Transient);
    }

    [Test]
    public async Task When_AddGateway_WithDefaultMessageMapper_Should_ApplyToEndpointsWithoutMapper()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhecerMessagingConfigurator(services);
        configurator.DefaultMessageMapper<TestMessageMapper>();

        var publication = Substitute.For<IPublication>();
        var subscription = Substitute.For<ISubscription>();

        configurator.AddGateway(CreateGateway(publications: [publication], subscriptions: [subscription]));

        await Assert.That(publication.MessageMapperType).IsEqualTo(typeof(TestMessageMapper));
        await Assert.That(subscription.MessageMapperType).IsEqualTo(typeof(TestMessageMapper));
    }

    [Test]
    public async Task When_AddGateway_WithDefaultMessageMapper_Should_NotOverrideConfiguredMapper()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhecerMessagingConfigurator(services);
        configurator.DefaultMessageMapper<TestMessageMapper>();

        var publication = Substitute.For<IPublication>();
        publication.MessageMapperType.Returns(typeof(AnotherMessageMapper));

        configurator.AddGateway(CreateGateway(publications: [publication]));

        await Assert.That(publication.MessageMapperType).IsEqualTo(typeof(AnotherMessageMapper));
    }

    [Test]
    public async Task When_AddGateway_WithoutDefaultMessageMapper_Should_LeaveMapperUnset()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhecerMessagingConfigurator(services);

        var publication = Substitute.For<IPublication>();
        var subscription = Substitute.For<ISubscription>();

        configurator.AddGateway(CreateGateway(publications: [publication], subscriptions: [subscription]));

        await Assert.That(publication.MessageMapperType).IsNull();
        await Assert.That(subscription.MessageMapperType).IsNull();
    }

    private static IGateway CreateGateway(
        IPublication[]? publications = null,
        ISubscription[]? subscriptions = null)
    {
        var gateway = Substitute.For<IGateway>();
        gateway.Publications.Returns(publications ?? []);
        gateway.Subscriptions.Returns(subscriptions ?? []);
        return gateway;
    }

    private sealed class TestMessageMapper : IMessageMapper
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

    private sealed class TestEncodeTransformer : IEncodeTransformer
    {
        public ValueTask EncodeAsync(Message message, AmanhecerContext context,
            Func<Message, AmanhecerContext, ValueTask> next)
        {
            return next(message, context);
        }
    }

    private sealed class TestDecodeTransformer : IDecodeTransformer
    {
        public ValueTask DecodeAsync(Message message, AmanhecerContext context,
            Func<Message, AmanhecerContext, ValueTask> next)
        {
            return next(message, context);
        }
    }
}
