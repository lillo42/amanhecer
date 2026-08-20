using System;
using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

public interface IConsumer
{
    ValueTask StartAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);
}