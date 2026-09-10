using Amanhecer.Abstractions;
using Amanhecer.Configurator;
using Amanhecer.Extensions;
using Amanhecer.Extensions.Hosting.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMqQuorum;

// Start the broker first with: docker compose -f docker-compose-rabbitmq.yaml up -d
// Override the broker address with the AMANHECER_RABBITMQ_URI environment variable.
var amqpUri = new Uri(Environment.GetEnvironmentVariable("AMANHECER_RABBITMQ_URI")
    ?? "amqp://guest:guest@localhost:5672");

const string exchangeName = "amanhecer.samples.quorum";
const string queueName = "amanhecer.samples.quorum.greetings";

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAmanhecer(amanhecer => amanhecer
    .AddRequestHandler<GreetingHandler>()
    .UsingMessagingGateway(messaging => messaging.UsingRabbitMQ(rabbitMq => rabbitMq
        .Connection(connection => connection.Uri(amqpUri))
        .Publications(publications => publications.AddPublication(publication => publication
            .RoutingKey(Topics.Greeting)
            .RabbitMqRoutingKey(Topics.Greeting)
            .Persistent()
            .Exchange(exchange => exchange
                .Name(exchangeName)
                .CreateIfNotExists(create => create.Type(ExchangeType.Topic).Durable(true)))))
        .Subscriptions(subscriptions => subscriptions.AddSubscription(subscription => subscription
            .ToRoutingKey(Topics.Greeting)
            .QueueName(queueName)
            .BufferSize(10)
            .CreateIfNotExists(create => create
                .Exchange(exchange => exchange
                    .Name(exchangeName)
                    .CreateIfNotExists(exchangeCreate => exchangeCreate.Type(ExchangeType.Topic).Durable(true)))
                .RoutingKey(Topics.Greeting)
                // Quorum queues must be durable and non-exclusive; the x-queue-type
                // argument declares the queue as a quorum (Raft-replicated) queue.
                .Durable(true)
                .QueueArgument("x-queue-type", "quorum")))))));

// Starts a consumer for every subscription together with the host.
builder.Services.AddAmanhecerHost();

using var host = builder.Build();
await host.StartAsync();

var dispatcher = host.Services.GetRequiredService<IDispatcher>();
foreach (var i in Enumerable.Range(1, 5))
{
    var greeting = new Greeting($"Hello from quorum queue #{i}");
    Console.WriteLine($"Posting: {greeting.Message}");
    await dispatcher.PostAsync(greeting);
}

Console.WriteLine("Posted 5 messages; waiting for the consumer to catch up...");
await Task.Delay(TimeSpan.FromSeconds(3));

await host.StopAsync();
