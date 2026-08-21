using System;
using System.Collections.Generic;
using System.Linq;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.Configurator;

public class AmanhecerMessagingConfigurator(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;
    public List<IGateway> Gateways { get; } = [];
    public Dictionary<string, IReadOnlyList<AmanhecerTransformerOptions>> TransformerPipeline { get; } = [];

    public AmanhecerMessagingConfigurator AddTransformerPipeline(
        string pipelineName,
        IReadOnlyList<AmanhecerTransformerOptions> options)
    {
        if (TransformerPipeline.ContainsKey(pipelineName))
        {
            throw new NotImplementedException();
        }

        TransformerPipeline[pipelineName] =
        [
            .. options
                .OrderBy(x => x.Order)
        ];
        return this;
    }

    public AmanhecerMessagingConfigurator AddGateway(IGateway gateway)
    {
        gateway.ProvisionerAsync().GetAwaiter().GetResult();

        Gateways.Add(gateway);
        Services.AddSingleton(gateway);
        return this;
    }
}