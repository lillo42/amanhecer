using Amanhecer.Abstractions;
using Amanhecer.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Simple;

var service = new ServiceCollection()
    .AddAmanhecer(a => a
        .AddRequestHandler<GreetingHandler>()
        .AddQueryHandler<AskHandler>())
    .BuildServiceProvider();

await using var scope = service.CreateAsyncScope();
var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

Console.Write("Your name: ");
var name = Console.ReadLine() ?? string.Empty;
dispatcher.Send(new Greeting(name));

Console.Write("Make a Question: ");
var question = Console.ReadLine() ?? string.Empty;
var answer = dispatcher.Query<Ask, string>(new Ask(question));
Console.WriteLine(answer);