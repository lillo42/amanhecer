using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Amanhencer.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhencer.Configurator;

public class AmanhencerConfigurator(IServiceCollection services)
{
    private readonly List<AmanhencerRoutingOptions> _routingConfigurators = [];

    public IEnumerable<AmanhencerRoutingOptions> RoutingConfigurators => _routingConfigurators;

    public AmanhencerConfigurator AddRequestHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors |
                                    DynamicallyAccessedMemberTypes.Interfaces |
                                    DynamicallyAccessedMemberTypes.PublicMethods)]
        TRequestHandler>(
        Action<AmanhencerRoutingConfigurator>? configure = null)
        where TRequestHandler : IRequestHandler
    {
        return AddRoutingKey(GetRoutingKey(typeof(TRequestHandler)), cfg =>
        {
            configure?.Invoke(cfg);
            cfg.UseHandler<TRequestHandler>();
        });
    }

    public AmanhencerConfigurator AddQueryHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors |
                                    DynamicallyAccessedMemberTypes.Interfaces |
                                    DynamicallyAccessedMemberTypes.PublicMethods)]
        TQueryHandler>(
        Action<AmanhencerRoutingConfigurator>? configure = null)
        where TQueryHandler : IQueryHandler
    {
        return AddRoutingKey(GetRoutingKey(typeof(TQueryHandler)), cfg =>
        {
            configure?.Invoke(cfg);
            cfg.UseHandler<TQueryHandler>();
        });
    }

    public AmanhencerConfigurator AddRoutingKey(string routingKey,
        Action<AmanhencerRoutingConfigurator>? configure = null)
    {
        var cfg = new AmanhencerRoutingConfigurator(routingKey, services);
        configure?.Invoke(cfg);

        _routingConfigurators.Add(cfg.ToOptions());
        return this;
    }

    public AmanhencerConfigurator SetExecutorStrategy(IExecutingStrategy executor)
    {
        services.AddSingleton(executor);
        return this;
    }
    

    private static string GetRoutingKey(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
        Type requestHandlerType)
    {
        Type? argType = null;
        var interfaces = requestHandlerType.GetInterfaces();
        foreach (var @interface in interfaces)
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

            if (argType != null)
            {
                // Improve message
                throw new Exception();
            }

            argType = @interface.GetGenericArguments()[0];
        }

        if (argType == null)
        {
            // TODO: improve message
            throw new ArgumentException("Invalid RequestType", nameof(requestHandlerType));
        }

        var attribute = argType.GetCustomAttribute<RoutingKeyAttribute>();
        if (attribute != null)
        {
            return attribute.RoutingKey;
        }

        return argType.FullName ?? argType.Name;
    }
}