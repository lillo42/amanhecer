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

## Kafka tests

`tests/Amanhecer.ConfluentKafka.Tests` and `tests/Amanhecer.Dekaf.Tests` follow the same
split: broker-free unit tests (producer/consumer mapping, offset commit behavior, provisioner
validation) and broker-backed messaging gateway contract tests. The Confluent tests run
against Redpanda; the Dekaf tests need a Kafka 4.0+ broker, because Dekaf's consumer requires
the KIP-848 consumer group protocol (unsupported by Redpanda). In CI they run in a `kafka`
job with both containers; locally, start them with the compose files at the repository root:

```bash
podman compose -f docker-compose-redpanda.yaml up -d   # Confluent tests, port 9092
podman compose -f docker-compose-kafka.yaml up -d      # Dekaf tests, port 29092
# or: docker compose -f ... up -d
```

The tests connect to `localhost:9092` (Confluent) and `localhost:29092` (Dekaf) by default;
set `AMANHECER_KAFKA_BOOTSTRAP_SERVERS` / `AMANHECER_DEKAF_BOOTSTRAP_SERVERS` to point at
different clusters.

```bash
dotnet run --project tests/Amanhecer.ConfluentKafka.Tests -f net10.0
dotnet run --project tests/Amanhecer.Dekaf.Tests -f net10.0
```

## Project layout

- `src/Amanhecer.Abstractions` — interfaces, base classes, attributes and contexts.
- `src/Amanhecer` — the dispatcher, pipeline, factories, configurators, messaging abstractions and DI extensions.
- `src/Amanhecer.RabbitMq` — RabbitMQ transport for the messaging gateway.
- `src/Amanhecer.ConfluentKafka` — Kafka transport for the messaging gateway, built on Confluent.Kafka.
- `src/Amanhecer.Dekaf` — Kafka transport for the messaging gateway, built on Dekaf (pure C#).
- `src/Amanhecer.Extensions.Hosting` — generic-host integration running the message consumers.
- `src/Amanhecer.OpenTelemetry` — OpenTelemetry instrumentation.
- `src/Amanhecer.Polly` — Polly resilience middleware.
- `src/Amanhecer.Extensions.Resilience` — `Microsoft.Extensions.Resilience` middleware with telemetry enrichment.
- `samples/Simple`, `samples/Middleware` — console examples.
- `samples/RabbitMqQuorum` — RabbitMQ example using a quorum queue.
- `tests/Amanhecer.Messaging.Base.Tests` — transport-agnostic messaging gateway contract tests; new transports inherit them.
- `tests/*` — unit and integration tests (TUnit).
