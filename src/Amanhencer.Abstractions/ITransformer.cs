using System;
using System.Threading.Tasks;

namespace Amanhencer.Abstractions;

public interface ITransformer
{
    void Initialize(object metadata);

    ValueTask ExecuteAsync(ExternalMessage message, IContext context, Func<IContext, ValueTask> next);
}