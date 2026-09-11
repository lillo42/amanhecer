using System;
using System.Collections.Generic;
using System.Linq;
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
/// <param name="pumpFactory">The factory that creates the message pump driving each consumer.</param>
public class ConsumerHostedService(IServiceProvider provider, IMessagePumpFactory pumpFactory) : IHostedService
{
    private CancellationTokenSource? _cancellationTokenSource;

    private readonly List<Task> _tasks = [];

    /// <summary>
    /// Provisions every registered gateway (see <see cref="IGateway.ProvisionerAsync"/>), then
    /// creates the consumers for all subscriptions and starts them. The gateways are singletons
    /// reused across stop/start cycles: they are disposed by the container, never by this
    /// service, so a service restarted after <see cref="StopAsync"/> keeps working with the same
    /// gateway instances.
    /// </summary>
    /// <param name="cancellationToken">A token that signals the start should be aborted.</param>
    /// <returns>A <see cref="Task"/> that completes when all consumers have started.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await StopAsync(cancellationToken);

        _cancellationTokenSource = new CancellationTokenSource();
        var gateways = provider.GetServices<IGateway>().ToArray();
        foreach (var gateway in gateways)
        {
            await gateway.ProvisionerAsync();

            foreach (var subscription in gateway.Subscriptions)
            {
                for (var i = 0; i < subscription.NumberOfConsumers; i++)
                {
                    var consumer = gateway.CreateConsumer(subscription);
                    var pump = pumpFactory.Create();

                    _tasks.Add(pump.ExecuteAsync(consumer, _cancellationTokenSource.Token));
                }
            }
        }
    }

    /// <summary>
    /// Stops all consumers started by this hosted service. The gateways are left alive: they
    /// are reused when the service starts again and are disposed with the container.
    /// </summary>
    /// <param name="cancellationToken">A token that signals the stop should be aborted.</param>
    /// <returns>A <see cref="Task"/> that completes when all consumers have stopped.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cancellationTokenSource == null)
        {
            return;
        }

        await _cancellationTokenSource.CancelAsync();

        try
        {
            await Task.WhenAll(_tasks);
        }
        catch (OperationCanceledException)
        {
        }

        _tasks.Clear();

        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;
    }
}