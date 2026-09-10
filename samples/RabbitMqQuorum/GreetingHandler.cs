using Amanhecer.Abstractions;

namespace RabbitMqQuorum;

public static class Topics
{
    public const string Greeting = "amanhecer.samples.quorum.greeting";
}

[RoutingKey(Topics.Greeting)]
public record Greeting(string Message);

public class GreetingHandler : RequestHandler<Greeting>
{
    public override ValueTask HandleAsync(Greeting request, AmanhecerContext context, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss}] Consumed from quorum queue: {request.Message}");
        return ValueTask.CompletedTask;
    }
}
