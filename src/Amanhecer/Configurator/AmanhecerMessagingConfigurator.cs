using System.Collections.Generic;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.Configurator;

public class AmanhecerMessagingConfigurator(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;
    internal List<IGateway> Gateways { get; } = [];

    public AmanhecerMessagingConfigurator AddGateway(IGateway gateway)
    {
        gateway.ProvisionerAsync().GetAwaiter().GetResult();

        Gateways.Add(gateway);
        Services.AddSingleton(gateway);
        return this;
    }
}