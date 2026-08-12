using Amanhecer.Abstractions;
using Amanhecer.Extensions;
using Amanhecer.Middlewares;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Middleware;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var services = new ServiceCollection();

services.AddLogging(builder => builder.AddConsole());

services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddConsoleExporter())
    .WithMetrics(metrics => metrics.AddConsoleExporter());

services.AddAmanhecer(a => a
    .AddRequestHandler<GreetingHandler>(cfg => cfg
        .Use<TimingMiddleware>(order: 1)
        .Use<AmanhecerTelemetryMiddleware>(order: 2)));

await using var service = services.BuildServiceProvider();

// Force the OpenTelemetry providers to start listening before dispatching;
// without a host, nothing resolves them otherwise.
_ = service.GetRequiredService<TracerProvider>();
_ = service.GetRequiredService<MeterProvider>();

await using var scope = service.CreateAsyncScope();
var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

Console.Write("Your name: ");
var name = Console.ReadLine() ?? string.Empty;
dispatcher.Send(new Greeting(name));
