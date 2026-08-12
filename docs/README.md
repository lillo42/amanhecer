# Amanhecer

A lightweight request dispatcher (mediator) for .NET, inspired by [Paramore Brighter](https://github.com/BrighterCommand/Brighter). Send commands, publish events and execute queries through configurable middleware pipelines — with no external broker required.

## Features

- **Send / Publish / Query** — dispatch a command to a single handler, an event to any number of handlers, or a query that returns a response.
- **Middleware pipelines** — wrap handlers with cross-cutting concerns (logging, validation, retries) configured fluently or via attributes.
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
| `Amanhecer` | The dispatcher, pipeline, factories, configurators and DI extensions. |
| `Amanhecer.OpenTelemetry` | OpenTelemetry trace and metric instrumentation for the request pipelines. |
| `Amanhecer.Polly` | Polly integration: run pipelines inside named resilience pipelines. |
| `Amanhecer.Extensions.Resilience` | `Microsoft.Extensions.Resilience` integration with request-metadata telemetry enrichment. |
