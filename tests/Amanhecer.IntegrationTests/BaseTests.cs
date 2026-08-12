using System;
using System.Threading.Tasks;
using Amanhecer.Configurator;
using Amanhecer.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.IntegrationTests;

public abstract class BaseTests
{
    protected IServiceProvider ServiceProvider { get; private set; } = null!;

    [Before(Test)]
    public void BeforeTests()
    {
        var services = new ServiceCollection();
        ConfigureServiceCollection(services);
        services
            .AddLogging()
            .AddAmanhecer(ConfigureAmanhecer);

        ServiceProvider = services.BuildServiceProvider();
    }

    [After(Test)]
    public async Task AfterTests()
    {
        if (ServiceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        ServiceProvider = null!;
    }

    protected virtual void ConfigureServiceCollection(IServiceCollection services)
    {
    }

    protected virtual void ConfigureAmanhecer(AmanhecerConfigurator configurator)
    {
    }
}