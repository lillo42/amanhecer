using Amanhecer.Abstractions;

namespace Simple;

public record Ask(string Question);

public class AskHandler : QueryHandler<Ask, string>
{
    public override ValueTask<string> HandleAsync(Ask @event, IPipelineContext context, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Received question {@event.Question}!");
        return ValueTask.FromResult("Good question");
    }
}