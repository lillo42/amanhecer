# Building and testing

## Building

```bash
dotnet build Amanhencer.slnx
```

## Testing

The test projects use TUnit with Microsoft.Testing.Platform. On the .NET 10 SDK, run the whole suite with:

```bash
dotnet test --solution Amanhencer.slnx
```

Note: do not pass `--nologo` — it is forwarded to the test apps, which reject it (exit code 5, "zero tests ran"). `--no-build` is likewise unsupported by the MTP runner.

Test projects can also be run directly as executables:

```bash
dotnet run --project tests/Amanhencer.Tests -f net10.0
dotnet run --project tests/Amanhencer.IntegrationTests -f net10.0
dotnet run --project tests/Amanhencer.Polly.Tests -f net10.0
dotnet run --project tests/Amanhencer.Extensions.Resilience.Tests -f net10.0
```

## Project layout

- `src/Amanhencer.Abstractions` — interfaces, base classes, attributes and contexts.
- `src/Amanhencer` — the dispatcher, pipeline, factories, configurators and DI extensions.
- `src/Amanhencer.OpenTelemetry` — OpenTelemetry instrumentation.
- `src/Amanhencer.Polly` — Polly resilience middleware.
- `src/Amanhencer.Extensions.Resilience` — `Microsoft.Extensions.Resilience` middleware with telemetry enrichment.
- `samples/Simple`, `samples/Middleware` — console examples.
- `tests/*` — unit and integration tests (TUnit).
