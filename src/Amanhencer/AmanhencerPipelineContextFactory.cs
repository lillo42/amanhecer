using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using Amanhencer.Abstractions;

namespace Amanhencer;

/// <summary>
/// Creates <see cref="IPipelineContext"/> instances, resolving the routing key from the
/// <see cref="IContext"/> or from the request type's <see cref="RoutingKeyAttribute"/>
/// (falling back to the request type's full name).
/// </summary>
/// <param name="defaultStrategy">The <see cref="IExecutingStrategy"/> used when the context does not specify one.</param>
public class AmanhencerPipelineContextFactory(IExecutingStrategy defaultStrategy) : IPipelineContextFactory
{
    private readonly ConcurrentDictionary<Type, string> _cacheRoutingKeys = new();
    
    /// <summary>
    /// Creates a pipeline context for the given request, copying the activity, metadata,
    /// routing key and executing strategy from the provided context.
    /// </summary>
    /// <param name="request">The request being processed.</param>
    /// <param name="context">The call context to build the pipeline context from.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A new pipeline context for the request.</returns>
    public IPipelineContext Create(object request, IContext context, CancellationToken cancellationToken)
    {
        return new AmanhencerPipelineContext(context.Activity, 
            context.TelemetryTags,
            context.Metadata,
            GetRoutingKey(context, request), 
            request, 
            context.ExecutingStrategy ?? defaultStrategy,
            context.ContinueOnCapturedContext,
            cancellationToken);
    }

    /// <summary>
    /// Resolves the routing key for the request, using the context's explicit routing key when set,
    /// otherwise the request type's <see cref="RoutingKeyAttribute"/> or full name (cached per type).
    /// </summary>
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