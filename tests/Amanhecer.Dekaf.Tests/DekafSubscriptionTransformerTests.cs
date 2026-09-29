using System;
using System.Linq;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Dekaf.Configurations;
using Amanhecer.Messaging.Transformers;

namespace Amanhecer.Dekaf.Tests;

public class DekafSubscriptionTransformerTests
{
    [Test]
    public async Task When_Transformer_With_Delegate_Should_Append_The_Anonymous_Decode_Transformer()
    {
        var cfg = new DekafSubscriptionConfigurator();
        cfg.ToRoutingKey("tests.routing");

        Func<
            Message,
            AmanhecerContext,
            Func<Message, AmanhecerContext, ValueTask>,
            ValueTask
        > func = (message, context, next) => next(message, context);
        cfg.Transformer(func, order: 6);

        var subscription = cfg.ToSubscription();

        var options = subscription.Transformers.Single(x =>
            x.TransformerType == typeof(AnonymousDecodeTransformer)
        );
        await Assert.That(options.Order).IsEqualTo(6);
        await Assert.That(options.Metadata).IsSameReferenceAs(func);
    }
}
