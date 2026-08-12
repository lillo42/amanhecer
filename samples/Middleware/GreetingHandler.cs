using Amanhecer.Abstractions;
using Amanhecer.Middlewares;

namespace Middleware;

public record Greeting(string Name);

public class GreetingHandler : RequestHandler<Greeting>
{
    [RequestLogging(3)]
    public override ValueTask HandleAsync(Greeting request, IPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Hello {request.Name}!");
        return ValueTask.CompletedTask;
    }
}
