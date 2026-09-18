# Amanhecer

A lightweight request dispatcher (mediator) for .NET. Send commands, publish events and execute queries through configurable middleware pipelines — with no external broker required.

## Features

- **Send / Publish / Query / Post** — dispatch a command to a single handler, an event to any number of handlers, a query that returns a response, or a message to a broker publication.
- **Middleware pipelines** — wrap handlers with cross-cutting concerns (logging, validation, retries) configured fluently or via attributes.
- **Messaging gateway** — publish and consume `Message`s through publications and subscriptions, with pluggable message mappers and encode/decode transformers.
- **RabbitMQ transport** — bind the messaging gateway to RabbitMQ exchanges and queues (classic, quorum, dead-letter topologies) via the `Amanhecer.RabbitMq` package.
- **Kafka transports** — bind the messaging gateway to Kafka topics and consumer groups via the `Amanhecer.ConfluentKafka` (Confluent.Kafka) or `Amanhecer.Dekaf` (Dekaf, pure C#) packages, with batched offset commits and pluggable topic provisioning.
- **Routing keys** — route requests to pipelines by convention (type name) or explicitly with `[RoutingKey]`.
- **Executing strategies** — run published pipelines sequentially or in parallel.
- **DI-first** — built on `Microsoft.Extensions.DependencyInjection`; everything is resolved from the container.
- **Multi-targeting** — `net462`, `netstandard2.0`, `net8.0`, `net9.0` and `net10.0`, AOT-compatible on modern targets.

## Quick start

Define a request and its handler:

```csharp
using Amanhecer.Abstractions;

public record Greeting(string Name);

public class GreetingHandler : RequestHandler<Greeting>
{
    public override ValueTask HandleAsync(Greeting request, AmanhecerContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Hello {request.Name}!");
        return ValueTask.CompletedTask;
    }
}
```

Register Amanhecer and dispatch:

```csharp
using Amanhecer.Abstractions;
using Amanhecer.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection()
    .AddAmanhecer(a => a
        .AddRequestHandler<GreetingHandler>()
        .AddQueryHandler<AskHandler>())
    .BuildServiceProvider();

var dispatcher = services.GetRequiredService<IDispatcher>();

dispatcher.Send(new Greeting("world"));                     // one handler
dispatcher.Publish(new OrderShipped(id));                   // any number of handlers
var answer = dispatcher.Query<Ask, string>(new Ask("?"));   // returns a response
```

All operations have `async` overloads (`SendAsync`, `PublishAsync`, `QueryAsync`, `PostAsync`) and accept an optional `AmanhecerContext` carrying a routing-key override, metadata, an `Activity` and a per-dispatch executing strategy.

## Middleware

Implement `IMiddleware` and add it to a pipeline when registering the handler:

```csharp
public class LoggingMiddleware : IMiddleware
{
    public async ValueTask ExecuteAsync(AmanhecerContext context,
        Func<AmanhecerContext, ValueTask> next)
    {
        Console.WriteLine($"--> {context.RoutingKey}");
        await next(context);
        Console.WriteLine($"<-- {context.RoutingKey}");
    }
}

services.AddAmanhecer(a => a
    .AddRequestHandler<GreetingHandler>(cfg => cfg.Use<LoggingMiddleware>(order: 1)));
```

Alternatively, declare middleware on the handler (class or `HandleAsync` method) by deriving from `MiddlewareAttribute`:

```csharp
public class LoggingAttribute(int order) : MiddlewareAttribute(order)
{
    public override Type GetMiddlewareType() => typeof(LoggingMiddleware);
}

[Logging(order: 1)]
public class GreetingHandler : RequestHandler<Greeting> { ... }
```

Lower `Order` values run earlier; the handler always runs last.

## Routing keys

By default a request is routed by its type's full name. Override with `[RoutingKey]`:

```csharp
[RoutingKey("priority.order")]
public record PlaceOrder(int Id);
```

or per dispatch via the context:

```csharp
dispatcher.Send(request, new AmanhecerContext { RoutingKey = "priority.order" });
```

`Send` and `Query` require exactly one matching pipeline (`PipelineNotFoundException` / `MultiPipelineFoundException` otherwise); `Publish` tolerates any number.

## Executing strategies

When `Publish` fans out to multiple pipelines, the default `SequenceExecutingStrategy` runs them in registration order. Switch to parallel execution globally:

```csharp
services.AddAmanhecer(a => a
    .SetExecutorStrategy(new ParallelExecutingStrategy(
        new ParallelOptions(),
        new AmanhecerPipelineContextAccessor(),
        NullLogger<ParallelExecutingStrategy>.Instance))
    .AddRequestHandler<GreetingHandler>());
```

## Project layout

- `src/Amanhecer.Abstractions` — interfaces, base classes, attributes and contexts.
- `src/Amanhecer` — the dispatcher, pipeline, factories, configurators, messaging abstractions and DI extensions.
- `src/Amanhecer.RabbitMq` — RabbitMQ transport for the messaging gateway.
- `src/Amanhecer.ConfluentKafka` — Kafka transport for the messaging gateway, built on Confluent.Kafka.
- `src/Amanhecer.Dekaf` — Kafka transport for the messaging gateway, built on Dekaf (pure C#).
- `src/Amanhecer.Extensions.Hosting` — generic-host integration that runs the message consumers as a hosted service.
- `src/Amanhecer.Polly` — middleware that wraps handlers in [Polly](https://www.pollydocs.org/) resilience pipelines.
- `src/Amanhecer.Extensions.Resilience` — the same resilience middleware built on `Microsoft.Extensions.Resilience`.
- `src/Amanhecer.OpenTelemetry` — OpenTelemetry tracing and metrics instrumentation for pipelines.
- `samples/Simple` — a minimal console example.
- `samples/Middleware` — a console example showing middleware in a pipeline.
- `samples/RabbitMqQuorum` — a RabbitMQ example publishing to and consuming from a quorum queue.
- `tests/Amanhecer.Tests` — unit tests (TUnit + NSubstitute).
- `tests/Amanhecer.IntegrationTests` — end-to-end tests through the real DI container and pipelines.
- `tests/Amanhecer.RabbitMq.Tests` — transport tests against a real broker (see `docker-compose-rabbitmq.yaml`).
- `tests/Amanhecer.Polly.Tests`, `tests/Amanhecer.Extensions.Resilience.Tests`, `tests/Amanhecer.OpenTelemetry.Tests` — tests for the extension packages.

## Building and testing

```bash
dotnet build Amanhecer.slnx
```

The test projects use TUnit with Microsoft.Testing.Platform, which `dotnet test` supports natively on the .NET 10 SDK:

```bash
dotnet test --solution Amanhecer.slnx
```

You can also run a single test project directly:

```bash
dotnet run --project tests/Amanhecer.Tests -f net10.0
dotnet run --project tests/Amanhecer.IntegrationTests -f net10.0
```

## Licence

[LGPL-3.0-or-later](LICENSE)
