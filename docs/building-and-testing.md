# Building and testing

## Building

```bash
dotnet build Amanhecer.slnx
```

## Testing

The test projects use TUnit with Microsoft.Testing.Platform. On the .NET 10 SDK, run the whole suite with:

```bash
dotnet test --solution Amanhecer.slnx
```

Note: do not pass `--nologo` — it is forwarded to the test apps, which reject it (exit code 5, "zero tests ran"). `--no-build` is likewise unsupported by the MTP runner.

Test projects can also be run directly as executables:

```bash
dotnet run --project tests/Amanhecer.Tests -f net10.0
dotnet run --project tests/Amanhecer.Abstractions.Tests -f net10.0
dotnet run --project tests/Amanhecer.Extensions.Hosting.Tests -f net10.0
dotnet run --project tests/Amanhecer.IntegrationTests -f net10.0
dotnet run --project tests/Amanhecer.OpenTelemetry.Tests -f net10.0
dotnet run --project tests/Amanhecer.Polly.Tests -f net10.0
dotnet run --project tests/Amanhecer.Extensions.Resilience.Tests -f net10.0
```

## RabbitMQ tests

`tests/Amanhecer.RabbitMq.Tests` contains two kinds of tests:

- **Broker-free unit tests** — configurator validation, producer properties, exchange
  provisioners and the unreachable-broker connection test run against mocks or an unused
  port, so they pass anywhere with no broker running.
- **Broker-backed integration tests** — the transport-agnostic messaging gateway contract
  tests from `tests/Amanhecer.Messaging.Base.Tests`, plus provisioning, dead-lettering,
  routing and redelivery tests, all run against a real broker.

In CI, the build workflow runs the broker-free test projects in a `build` job first and, once
it passes, a dependent `rabbitmq` job runs `tests/Amanhecer.RabbitMq.Tests` against a
`rabbitmq:4-management` service container. Locally, start one with the compose file at the
repository root (works with both Docker and Podman):

```bash
podman compose -f docker-compose-rabbitmq.yaml up -d
# or: docker compose -f docker-compose-rabbitmq.yaml up -d
```

The tests connect to `amqp://guest:guest@localhost:5672` by default; set the
`AMANHECER_RABBITMQ_URI` environment variable to point at a different broker. The management UI
is exposed on <http://localhost:15672> (guest/guest).

```bash
dotnet run --project tests/Amanhecer.RabbitMq.Tests -f net10.0
```

## Project layout

- `src/Amanhecer.Abstractions` — interfaces, base classes, attributes and contexts.
- `src/Amanhecer` — the dispatcher, pipeline, factories, configurators, messaging abstractions and DI extensions.
- `src/Amanhecer.RabbitMq` — RabbitMQ transport for the messaging gateway.
- `src/Amanhecer.Extensions.Hosting` — generic-host integration running the message consumers.
- `src/Amanhecer.OpenTelemetry` — OpenTelemetry instrumentation.
- `src/Amanhecer.Polly` — Polly resilience middleware.
- `src/Amanhecer.Extensions.Resilience` — `Microsoft.Extensions.Resilience` middleware with telemetry enrichment.
- `samples/Simple`, `samples/Middleware` — console examples.
- `samples/RabbitMqQuorum` — RabbitMQ example using a quorum queue.
- `tests/Amanhecer.Messaging.Base.Tests` — transport-agnostic messaging gateway contract tests; new transports inherit them.
- `tests/*` — unit and integration tests (TUnit).
