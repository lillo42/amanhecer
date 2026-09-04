using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Configurator;
using Amanhecer.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.OpenTelemetry.Tests;

public record PlaceOrder(string Product);

public class PlaceOrderHandler : RequestHandler<PlaceOrder>
{
    public override ValueTask HandleAsync(PlaceOrder request, AmanhecerContext context, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}

public class ExplodingOrderHandler : RequestHandler<PlaceOrder>
{
    public override ValueTask HandleAsync(PlaceOrder request, AmanhecerContext context, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Handler exploded.");
    }
}

public static class DispatcherFixture
{
    public static IDispatcher Create(Action<AmanhecerConfigurator> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAmanhecer(configure);
        return services.BuildServiceProvider().GetRequiredService<IDispatcher>();
    }
}
