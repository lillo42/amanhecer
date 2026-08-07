using Amanhencer.Abstractions;
using Amanhencer.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Simple;

var service = new ServiceCollection()
    .AddAmanhencer(a => a
        .AddRequestHandler<GreetingHandler>()
        .AddQueryHandler<AskHandler>())
    .BuildServiceProvider();

await using var scope = service.CreateAsyncScope();
var process = scope.ServiceProvider.GetRequiredService<IProcessor>();

Console.Write("Your name: ");
var name = Console.ReadLine() ?? string.Empty;
process.Send(new Greeting(name));

Console.Write("Make a Question: ");
var question = Console.ReadLine() ?? string.Empty;
var answer = process.Query<Ask, string>(new Ask(question));
Console.WriteLine(answer);