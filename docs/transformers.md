# Transformers

Transformers wrap the mapping between application requests and `Message`s when messages are
published to or consumed from a messaging gateway. Each transformer can run code before and
after calling the next step — the same Russian-doll model as [middleware](middleware.md), but
around the message instead of the handler.

There are two directions, and each transformer opts into one or both:

```csharp
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

// Runs only when publishing.
public class CompressTransformer : IEncodeTransformer
{
    public async ValueTask EncodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        message.Payload = Compress(message.Payload);
        await next(message, context);
    }
}

// Runs only when consuming.
public class ValidateTransformer : IDecodeTransformer { /* DecodeAsync ... */ }

// Runs in both directions — use for symmetric transforms like compression or encryption,
// so the pair is registered and ordered together.
public class GZipTransformer : ITransformer { /* EncodeAsync and DecodeAsync ... */ }
```

The metadata supplied at registration time is stored in `AmanhecerContext.Metadata` when the
transformer is created; read it with `context.GetMetadata<T>()`.

## Per-publication registration

Add transformers to a publication when configuring it:

```csharp
publications.AddPublication(p => p
    .Name("orders")
    .RoutingKey("orders")
    .RabbitMqRoutingKey("orders.created")
    .Exchange(ex => ex.Name("orders"))
    .MessageMapper<OrderMapper>()
    .Transformer<SetCloudEventTransformer>(order: 0)
    .Transformer<CompressTransformer>(order: 10));
```

Per-publication transformers apply to the encode pipeline of that publication only.

## Global registration

Register a transformer once for every publication and subscription:

```csharp
services.AddAmanhecer(cfg => cfg
    .UsingMessagingGateway(m => m
        .AddGlobalTransformer<SetCloudEventTransformer>(order: 0)
        .AddGlobalTransformer<LoggingTransformer>(order: 100)
        .AddGateway(/* ... */)));
```

A global transformer contributes to the direction(s) it implements: encode pipelines only run
`IEncodeTransformer`s, decode pipelines only `IDecodeTransformer`s.

## Attribute registration

Derive from `TransformerAttribute` and annotate the message mapper. The transformer applies to
the pipelines of every publication and subscription using that mapper:

```csharp
public class CompressAttribute(int order) : TransformerAttribute(order)
{
    public override Type GetTransformerType() => typeof(CompressTransformer);
}

[Compress(order: 10)]
public class OrderMapper : IMessageMapper<OrderCreated> { ... }
```

With attribute registration, the attribute instance itself is stored as the transformer's
metadata, so the attribute can carry configuration to the transformer — see
`CloudEventAttribute` / `SetCloudEventTransformer` in the `Amanhecer` package for an example.

## Ordering and merging

Lower `Order` values run earlier. Transformers from all sources — global registration,
mapper attributes and per-publication configuration — are merged into a single ordered
pipeline for each direction. Subscriptions expose the same per-instance `Transformers`
list as publications, applied to the decode pipeline.

The `Amanhecer` package ships two built-in transformers: `SetCloudEventTransformer`, which
applies the configured CloudEvents attributes and the publication/subscription defaults, and
`StructuredCloudEventTransformer`, which wraps and unwraps the CloudEvents structured (JSON)
envelope — the RabbitMQ configurators register it automatically when structured content mode
is used (see [RabbitMQ](rabbitmq.md#cloudevents-content-modes)).

## Named pipelines

For full control, pipelines can also be registered explicitly by name:

```csharp
services.AddAmanhecer(cfg => cfg
    .UsingMessagingGateway(m => m.AddTransformerPipeline(
        "Amanhecer.Messaging.Transformer.Encode.orders",
        [new AmanhecerTransformerOptions(typeof(CompressTransformer), 10, null)])));
```

The encode pipeline of a publication named `orders` is
`Amanhecer.Messaging.Transformer.Encode.orders`; the decode pipeline of a subscription named
`orders-sub` is `Amanhecer.Messaging.Transformer.Decode.orders-sub`. Prefer the registration
styles above — they derive these names for you.
