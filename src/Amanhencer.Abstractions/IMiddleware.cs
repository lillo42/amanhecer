using System;
using System.Threading.Tasks;

namespace Amanhencer.Abstractions;

public interface IMiddleware
{
    void Initialize(object? metadata);

    ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next);
}