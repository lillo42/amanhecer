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
dotnet run --project tests/Amanhecer.Polly.Tests -f net10.0
dotnet run --project tests/Amanhecer.Extensions.Resilience.Tests -f net10.0
```

## RabbitMQ tests

`tests/Amanhecer.RabbitMq.Tests` runs the transport-agnostic messaging gateway contract tests
from `tests/Amanhecer.Messaging.Base.Tests` against a real broker. Start one with the compose file at the
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
- `src/Amanhecer` — the dispatcher, pipeline, factories, configurators and DI extensions.
- `src/Amanhecer.OpenTelemetry` — OpenTelemetry instrumentation.
- `src/Amanhecer.Polly` — Polly resilience middleware.
- `src/Amanhecer.Extensions.Resilience` — `Microsoft.Extensions.Resilience` middleware with telemetry enrichment.
- `samples/Simple`, `samples/Middleware` — console examples.
- `tests/Amanhecer.Messaging.Base.Tests` — transport-agnostic messaging gateway contract tests; new transports inherit them.
- `tests/*` — unit and integration tests (TUnit).
