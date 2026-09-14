# Amanhecer

A lightweight request dispatcher (mediator) for .NET. Send commands, publish events and execute queries through configurable middleware pipelines — in-process by default, or through a message broker with the messaging gateway.

## Features

- **Send / Publish / Query / Post** — dispatch a command to a single handler, an event to any number of handlers, a query that returns a response, or a message to a broker publication.
- **Middleware pipelines** — wrap handlers with cross-cutting concerns (logging, validation, retries) configured fluently or via attributes.
- **Messaging gateway** — publish and consume `Message`s through publications and subscriptions, with pluggable message mappers and encode/decode transformers (CloudEvents mapping included).
- **In-memory transport** — the `Amanhecer.InMemory` package binds the messaging gateway to in-memory queues (channel-backed), ideal for tests, local workflows and broker-free deployments.
- **RabbitMQ transport** — the `Amanhecer.RabbitMq` package binds the messaging gateway to RabbitMQ exchanges and queues, with pluggable provisioning (classic, quorum, dead-letter topologies).
- **Routing keys** — route requests to pipelines by convention (type name) or explicitly with `[RoutingKey]`.
- **Executing strategies** — run published pipelines sequentially or in parallel.
- **Resilience** — execute pipelines inside named Polly resilience pipelines via the `Amanhecer.Polly` or `Amanhecer.Extensions.Resilience` packages.
- **Observability** — built-in `ActivitySource` and `Meter`, with an `Amanhecer.OpenTelemetry` integration package.
- **DI-first** — built on `Microsoft.Extensions.DependencyInjection`; everything is resolved from the container.
- **Multi-targeting** — `net462`, `netstandard2.0`, `net8.0`, `net9.0` and `net10.0`, AOT-compatible on modern targets.

## Packages

| Package | Description |
| --- | --- |
| `Amanhecer.Abstractions` | Interfaces, base classes, attributes and contexts. |
| `Amanhecer` | The dispatcher, pipeline, factories, configurators, messaging abstractions and DI extensions. |
| `Amanhecer.InMemory` | In-memory transport for the messaging gateway: channel-backed queues, publications, subscriptions and provisioning. |
| `Amanhecer.RabbitMq` | RabbitMQ transport for the messaging gateway: exchanges, queues, provisioning and consumption. |
| `Amanhecer.Extensions.Hosting` | Generic-host integration: runs the message consumers as a hosted service. |
| `Amanhecer.OpenTelemetry` | OpenTelemetry trace and metric instrumentation for the request pipelines. |
| `Amanhecer.Polly` | Polly integration: run pipelines inside named resilience pipelines. |
| `Amanhecer.Extensions.Resilience` | `Microsoft.Extensions.Resilience` integration with request-metadata telemetry enrichment. |
