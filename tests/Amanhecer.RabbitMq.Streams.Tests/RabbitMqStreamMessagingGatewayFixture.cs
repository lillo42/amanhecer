using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using RabbitMQ.Stream.Client;
using IConsumer = Amanhecer.Abstractions.Messaging.IConsumer;
using Message = Amanhecer.Abstractions.Messaging.Message;

namespace Amanhecer.RabbitMq.Streams.Tests;

/// <summary>
/// Helpers for the broker-backed RabbitMQ Streams tests: connection settings, isolated stream
/// names, message creation and stream cleanup. Each test gets its own stream, so tests stay
/// isolated and can run in parallel.
/// </summary>
internal static class RabbitMqStreamMessagingGatewayFixture
{
    private static readonly string Host;
    private static readonly int Port;

    static RabbitMqStreamMessagingGatewayFixture()
    {
        // Defaults to the broker started by the repository's docker-compose-rabbitmq.yaml
        // (with the rabbitmq_stream plugin enabled); override with the
        // AMANHECER_RABBITMQ_STREAM_ENDPOINT environment variable, in host[:port] form.
        var endpoint = Environment.GetEnvironmentVariable("AMANHECER_RABBITMQ_STREAM_ENDPOINT") ?? "localhost:5552";
        var separator = endpoint.LastIndexOf(':');
        if (separator > 0 && int.TryParse(endpoint[(separator + 1)..], out var port))
        {
            Host = endpoint[..separator];
            Port = port;
        }
        else
        {
            Host = endpoint;
            Port = 5552;
        }
    }

    /// <summary>Creates a fresh endpoint list pointing at the test broker.</summary>
    /// <returns>The endpoints gateways and stream systems connect with.</returns>
    public static IList<EndPoint> CreateEndPoints()
    {
        return [new DnsEndPoint(Host, Port)];
    }

    /// <summary>Creates a connected <see cref="StreamSystem"/> against the test broker.</summary>
    /// <returns>The stream system; the caller closes it.</returns>
    public static async Task<StreamSystem> CreateStreamSystemAsync()
    {
        return await StreamSystem.Create(new StreamSystemConfig
        {
            UserName = "guest",
            Password = "guest",
            Endpoints = CreateEndPoints()
        });
    }

    /// <summary>
    /// Deletes a stream, tolerating a stream that is already gone.
    /// </summary>
    /// <param name="streamName">The stream to delete.</param>
    public static async Task DeleteStreamAsync(string streamName)
    {
        var system = await CreateStreamSystemAsync();
        try
        {
            await system.DeleteStream(streamName);
        }
        catch (DeleteStreamException)
        {
            // The stream is already gone: nothing to clean up.
        }
        finally
        {
            await system.Close();
        }
    }

    /// <summary>
    /// Creates the message produced by the tests: a message with a unique id and payload.
    /// </summary>
    /// <returns>A new message.</returns>
    public static Message CreateMessage()
    {
        return new Message
        {
            ContentType = new ContentType("text/plain"),
            Source = new Uri("amanhecer.tests", UriKind.RelativeOrAbsolute),
            SpecVersion = "1.0",
            Type = "amanhecer.tests.message",
            Payload = Encoding.UTF8.GetBytes(Uuid.NewGuid().ToString())
        };
    }

    /// <summary>
    /// Receives the next message from the consumer, failing after
    /// <paramref name="timeout"/> when none arrives.
    /// </summary>
    /// <param name="consumer">The consumer that receives the message.</param>
    /// <param name="timeout">How long to wait for the message.</param>
    /// <returns>The received message.</returns>
    public static async Task<Message> ReceiveOneAsync(IConsumer consumer, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var messages = await consumer.GetMessagesAsync(cts.Token);
        return messages[0];
    }
}
