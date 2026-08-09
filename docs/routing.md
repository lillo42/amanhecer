# Routing keys

Every dispatch is routed to pipelines by a routing key. By default the key is the request type's full name.

## Declaring a routing key

Override the convention with `[RoutingKey]` on the request or query type:

```csharp
using Amanhencer.Abstractions;

[RoutingKey("priority.order")]
public record PlaceOrder(int Id);
```

## Overriding per dispatch

Pass an `IContext` with a `RoutingKey` to route a request to a different pipeline:

```csharp
dispatcher.Send(request, new AmanhencerContext { RoutingKey = "priority.order" });
```

## Registering a pipeline for a routing key

Several handlers can share a routing key, and a routing key can be registered without a handler type:

```csharp
services.AddAmanhencer(a => a
    .AddRoutingKey("priority.order", cfg => cfg
        .Use<LoggingMiddleware>(order: 1)
        .UseHandler<PriorityOrderHandler>()));
```

## Matching rules

- `Send` and `Query` require **exactly one** matching pipeline: `PipelineNotFoundException` when none matches, `MultiPipelineFoundException` when more than one matches.
- `Publish` tolerates **any number** of matching pipelines, including zero.
