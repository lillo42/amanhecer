using System.Diagnostics;
using Amanhecer.Abstractions;

namespace Middleware;

/// <summary>
/// A custom middleware that measures how long the rest of the pipeline takes
/// and prints it to the console.
/// </summary>
public class TimingMiddleware : IMiddleware
{
    public void Initialize(object? metadata)
    {
    }

    public async ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            Console.WriteLine($"[TimingMiddleware] {context.RoutingKey} took {stopwatch.ElapsedMilliseconds} ms");
        }
    }
}
