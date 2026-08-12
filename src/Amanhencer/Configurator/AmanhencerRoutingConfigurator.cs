using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using Amanhencer.Abstractions;
using Amanhencer.Middlewares;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Amanhencer.Configurator;

/// <summary>
/// Configures the pipeline for a single routing key: the middlewares and the terminal handler.
/// </summary>
/// <param name="routingKey">The routing key handled by this pipeline.</param>
/// <param name="services">The service collection where middlewares and handlers are registered.</param>
public class AmanhencerRoutingConfigurator(string routingKey, IServiceCollection services)
{
    private Type? _handlerType;
    private readonly List<AmanhencerMiddlewareOptions> _middlewareOption = [];

    /// <summary>
    /// Adds a middleware to the pipeline.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type.</typeparam>
    /// <param name="order">The execution order within the pipeline; lower values run first.</param>
    /// <param name="metadata">Optional metadata passed to the middleware on initialisation.</param>
    /// <returns>The current <see cref="AmanhencerRoutingConfigurator"/>, for chaining.</returns>
    public AmanhencerRoutingConfigurator Use<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TMiddleware>(int order = 0, object? metadata = null)
        where TMiddleware : IMiddleware
    {
        Use(typeof(TMiddleware), order, metadata);
        return this;
    }

    /// <summary>
    /// Adds a middleware to the pipeline.
    /// </summary>
    /// <param name="middlewareType">The middleware type.</param>
    /// <param name="order">The execution order within the pipeline; lower values run first.</param>
    /// <param name="metadata">Optional metadata passed to the middleware on initialisation.</param>
    /// <returns>The current <see cref="AmanhencerRoutingConfigurator"/>, for chaining.</returns>
    public AmanhencerRoutingConfigurator Use(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type middlewareType, int order = 0, object? metadata = null)
    {
        _middlewareOption.Add(new AmanhencerMiddlewareOptions(middlewareType, order, metadata));
        services.TryAddTransient(middlewareType);
        return this;
    }

    /// <summary>
    /// Sets the terminal handler of the pipeline and registers the middlewares declared via
    /// <see cref="MiddlewareAttribute"/> on the handler class and its HandleAsync method.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <returns>The current <see cref="AmanhencerRoutingConfigurator"/>, for chaining.</returns>
    public AmanhencerRoutingConfigurator UseHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors |
                                    DynamicallyAccessedMemberTypes.Interfaces |
                                    DynamicallyAccessedMemberTypes.PublicMethods)]
        THandler>()
        where THandler : IHandler
    {
        return UseHandler(typeof(THandler));
    }

    /// <summary>
    /// Sets the terminal handler of the pipeline and registers the middlewares declared via
    /// <see cref="MiddlewareAttribute"/> on the handler class and its HandleAsync method.
    /// </summary>
    /// <param name="handlerType">The handler type.</param>
    /// <returns>The current <see cref="AmanhencerRoutingConfigurator"/>, for chaining.</returns>
    public AmanhencerRoutingConfigurator UseHandler(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors |
                                    DynamicallyAccessedMemberTypes.Interfaces |
                                    DynamicallyAccessedMemberTypes.PublicMethods)]
        Type handlerType)
    {
        _handlerType = handlerType;
        services.TryAddTransient(handlerType);
        AddMiddlewareFromClassAttribute(handlerType);
        AddMiddlewareFromCMethodAttribute(handlerType);
        return this;
    }

    /// <summary>
    /// Registers the middlewares declared with <see cref="MiddlewareAttribute"/> on the handler class.
    /// </summary>
    private void AddMiddlewareFromClassAttribute(Type handlerType)
    {
        var attributes = handlerType.GetCustomAttributes<MiddlewareAttribute>();
        foreach (var attribute in attributes)
        {
            Use(attribute.GetMiddlewareType(), attribute.Order, attribute);
        }
    }

    /// <summary>
    /// Registers the middlewares declared with <see cref="MiddlewareAttribute"/> on the handler's
    /// HandleAsync method.
    /// </summary>
    private void AddMiddlewareFromCMethodAttribute(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces |
                                    DynamicallyAccessedMemberTypes.PublicMethods)]
        Type handlerType)
    {
        Type? paramType = null;
        foreach (var @interface in handlerType.GetInterfaces())
        {
            if (!@interface.IsGenericType)
            {
                continue;
            }

            var genericTypeDefinition = @interface.GetGenericTypeDefinition();
            if (genericTypeDefinition != typeof(IRequestHandler<>) &&
                genericTypeDefinition != typeof(IQueryHandler<,>))
            {
                continue;
            }

            if (paramType != null)
            {
                return;
            }

            paramType = @interface.GetGenericArguments()[0];
        }

        if (paramType == null)
        {
            return;
        }

        var method = handlerType.GetMethod("HandleAsync",
            [paramType, typeof(IPipelineContext), typeof(CancellationToken)]);
        
        if (method == null)
        {
            return;
        }
        
        var attributes = method.GetCustomAttributes<MiddlewareAttribute>();
        foreach (var attribute in attributes)
        {
            Use(attribute.GetMiddlewareType(), attribute.Order, attribute);
        }
    }

    /// <summary>
    /// Builds the routing options for this pipeline, appending the terminal
    /// <see cref="ExecuteHandlerMiddleware"/> for the configured handler.
    /// </summary>
    /// <returns>The routing options for this pipeline.</returns>
    public AmanhencerRoutingOptions ToOptions()
    {
        Use<ExecuteHandlerMiddleware>(int.MaxValue, _handlerType);
        return new AmanhencerRoutingOptions(routingKey, _middlewareOption
            .OrderBy(x => x.Order));
    }
}