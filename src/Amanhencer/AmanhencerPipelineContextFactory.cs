using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using Amanhencer.Abstractions;

namespace Amanhencer;

public class AmanhencerPipelineContextFactory(IExecutingStrategy defaultStrategy) : IPipelineContextFactory
{
    private readonly ConcurrentDictionary<Type, string> _cacheRoutingKeys = new();
    
    public IPipelineContext Create(object request, IContext context, CancellationToken cancellationToken)
    {
        return new AmanhencerPipelineContext(context.Activity, 
            context.Metadata,
            GetRoutingKey(context, request), 
            request, 
            context.ExecutingStrategy ?? defaultStrategy,
            cancellationToken);
    }

    private string GetRoutingKey(IContext context, object request)
    {
        if (context.RoutingKey != null)
        {
            return context.RoutingKey;
        }

        return _cacheRoutingKeys.GetOrAdd(request.GetType(), type =>
        {
            var attribute = type.GetCustomAttribute<RoutingKeyAttribute>();
            return attribute == null ? (type.FullName ?? type.Name) : attribute.RoutingKey;
        });
    }
}