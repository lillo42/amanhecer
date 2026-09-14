using Amanhecer.Abstractions;
using Amanhecer.Configurator;
using Amanhecer.Extensions;
using Amanhecer.Extensions.Hosting.Extensions;
using InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// No broker needed: the in-memory transport keeps the queues (channel-backed) inside this
// process, so the publisher and the consumer must share the same host. Neither side sets a
// queue name: both default to the routing key, so they meet on the same queue.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAmanhecer(amanhecer => amanhecer
    .AddRequestHandler<GreetingHandler>()
    .UsingMessagingGateway(messaging => messaging.UsingInMemory(inMemory => inMemory
        .Publications(publications => publications.AddPublication(publication => publication
            .RoutingKey(Topics.Greeting)
            .CreateOrOverride(create => create.Capacity(1024))))
        .Subscriptions(subscriptions => subscriptions.AddSubscription(subscription => subscription
            .ToRoutingKey(Topics.Greeting)
            .BufferSize(10)
            .CreateOrOverride(create => create.Capacity(1024)))))));

// Starts a consumer for every subscription together with the host.
builder.Services.AddAmanhecerHost();

using var host = builder.Build();
await host.StartAsync();

var dispatcher = host.Services.GetRequiredService<IDispatcher>();
foreach (var i in Enumerable.Range(1, 5))
{
    var greeting = new Greeting($"Hello from the in-memory queue #{i}");
    Console.WriteLine($"Posting: {greeting.Message}");
    await dispatcher.PostAsync(greeting);
}

Console.WriteLine("Posted 5 messages; waiting for the consumer to catch up...");
await Task.Delay(TimeSpan.FromSeconds(3));

await host.StopAsync();
