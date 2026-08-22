using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Amanhecer.Extensions.Hosting;

/// <summary>
/// A hosted service that starts an <see cref="IConsumer"/> for every subscription of every
/// registered <see cref="IGateway"/> when the host starts, and stops them when the host shuts
/// down.
/// </summary>
/// <param name="provider">The service provider used to resolve the registered gateways and to
/// start the consumers.</param>
public class ConsumerHostedService(IServiceProvider provider) : IHostedService
{
    private readonly List<IConsumer> _consumers = [];

    /// <summary>
    /// Creates the consumers for all subscriptions of all registered gateways and starts them.
    /// </summary>
    /// <param name="cancellationToken">A token that signals the start should be aborted.</param>
    /// <returns>A <see cref="Task"/> that completes when all consumers have started.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var gateways = provider.GetServices<IGateway>();
        foreach (var gateway in gateways)
        {
            foreach (var subscription in gateway.Subscriptions)
            {
                for (var i = 0; i < subscription.NumberOfConsumer; i++)
                {
                    _consumers.Add(gateway.CreateConsumer(subscription));
                }
            }
        }

        foreach (var consumer in _consumers)
        {
            await consumer.StartAsync(provider, cancellationToken);
        }
    }

    /// <summary>
    /// Stops all consumers started by this hosted service.
    /// </summary>
    /// <param name="cancellationToken">A token that signals the stop should be aborted.</param>
    /// <returns>A <see cref="Task"/> that completes when all consumers have stopped.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var consumer in _consumers)
        {
            await consumer.StopAsync(cancellationToken);
        }
    }
}
