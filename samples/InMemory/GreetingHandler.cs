using Amanhecer.Abstractions;

namespace InMemory;

public static class Topics
{
    public const string Greeting = "amanhecer.samples.inmemory.greeting";
}

[RoutingKey(Topics.Greeting)]
public record Greeting(string Message);

public class GreetingHandler : RequestHandler<Greeting>
{
    public override ValueTask HandleAsync(Greeting request, AmanhecerContext context, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss}] Consumed from in-memory queue: {request.Message}");
        return ValueTask.CompletedTask;
    }
}
