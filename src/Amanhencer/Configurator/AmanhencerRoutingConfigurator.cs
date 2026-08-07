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

public class AmanhencerRoutingConfigurator(string routingKey, IServiceCollection services)
{
    private Type? _handlerType;
    private readonly List<AmanhencerMiddlewareOptions> _middlewareOption = new();

    public AmanhencerRoutingConfigurator Use<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TMiddleware>(int order = 0, object? metadata = null)
        where TMiddleware : IMiddleware
    {
        Use(typeof(TMiddleware), order, metadata);
        return this;
    }

    public AmanhencerRoutingConfigurator Use(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type middlewareType, int order = 0, object? metadata = null)
    {
        _middlewareOption.Add(new AmanhencerMiddlewareOptions(middlewareType, order, metadata));
        services.TryAddTransient(middlewareType);
        return this;
    }

    public AmanhencerRoutingConfigurator UseHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors |
                                    DynamicallyAccessedMemberTypes.Interfaces |
                                    DynamicallyAccessedMemberTypes.PublicMethods)]
        THandler>()
        where THandler : IHandler
    {
        return UseHandler(typeof(THandler));
    }

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

    private void AddMiddlewareFromClassAttribute(Type handlerType)
    {
        var attributes = handlerType.GetCustomAttributes<MiddlewareAttribute>();
        foreach (var attribute in attributes)
        {
            Use(attribute.GetMiddlewareType(), attribute.Order, attribute);
        }
    }

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

    public AmanhencerRoutingOptions ToOptions()
    {
        Use<ExecuteHandlerMiddleware>(int.MaxValue, _handlerType);
        return new AmanhencerRoutingOptions(routingKey, _middlewareOption
            .OrderBy(x => x.Order));
    }
}