using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Configurator;
using Amanhencer.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhencer.OpenTelemetry.Tests;

public record PlaceOrder(string Product);

public class PlaceOrderHandler : RequestHandler<PlaceOrder>
{
    public override ValueTask HandleAsync(PlaceOrder request, IPipelineContext context, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}

public class ExplodingOrderHandler : RequestHandler<PlaceOrder>
{
    public override ValueTask HandleAsync(PlaceOrder request, IPipelineContext context, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Handler exploded.");
    }
}

public static class DispatcherFixture
{
    public static IDispatcher Create(Action<AmanhencerConfigurator> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAmanhencer(configure);
        return services.BuildServiceProvider().GetRequiredService<IDispatcher>();
    }
}
