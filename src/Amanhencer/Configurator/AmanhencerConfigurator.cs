using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Amanhencer.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Amanhencer.Configurator;

/// <summary>
/// Configures Amanhencer: registers handlers, routing keys and the executing strategy
/// into the service collection.
/// </summary>
/// <param name="services">The service collection where handlers and middlewares are registered.</param>
public class AmanhencerConfigurator(IServiceCollection services)
{
    private readonly List<AmanhencerRoutingOptions> _routingConfigurators = [];

    /// <summary>
    /// Gets the routing options built from the configured routing keys.
    /// </summary>
    public IEnumerable<AmanhencerRoutingOptions> RoutingConfigurators => _routingConfigurators;

    /// <summary>
    /// Registers a request handler and creates a pipeline for the routing key derived from its request type.
    /// </summary>
    /// <typeparam name="TRequestHandler">The request handler type to register.</typeparam>
    /// <param name="configure">An optional action to configure the pipeline's middlewares.</param>
    /// <returns>The current <see cref="AmanhencerConfigurator"/>, for chaining.</returns>
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

    /// <summary>
    /// Registers a query handler and creates a pipeline for the routing key derived from its query type.
    /// </summary>
    /// <typeparam name="TQueryHandler">The query handler type to register.</typeparam>
    /// <param name="configure">An optional action to configure the pipeline's middlewares.</param>
    /// <returns>The current <see cref="AmanhencerConfigurator"/>, for chaining.</returns>
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

    /// <summary>
    /// Creates a pipeline for an explicit routing key.
    /// </summary>
    /// <param name="routingKey">The routing key the pipeline handles.</param>
    /// <param name="configure">An optional action to configure the pipeline's middlewares and handler.</param>
    /// <returns>The current <see cref="AmanhencerConfigurator"/>, for chaining.</returns>
    public AmanhencerConfigurator AddRoutingKey(string routingKey,
        Action<AmanhencerRoutingConfigurator>? configure = null)
    {
        var cfg = new AmanhencerRoutingConfigurator(routingKey, services);

        configure?.Invoke(cfg);

        _routingConfigurators.Add(cfg.ToOptions());
        return this;
    }

    /// <summary>
    /// Registers the default <see cref="IExecutingStrategy"/> used when a context does not specify one.
    /// </summary>
    /// <param name="executor">The executing strategy to register as a singleton.</param>
    /// <returns>The current <see cref="AmanhencerConfigurator"/>, for chaining.</returns>
    public AmanhencerConfigurator SetExecutorStrategy(IExecutingStrategy executor)
    {
        services.AddSingleton(executor);
        return this;
    }

    public AmanhencerConfigurator AutoFromAssemblies(params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x => !x.FullName.StartsWith("Amanhencer"))
                .ToArray();
        }

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes();
            foreach (var type in types)
            {
                if (IsMiddleware(type))
                {
                    services.TryAddTransient(type);
                }
                else if (IsHandler(type))
                {
                    AddRoutingKey(GetRoutingKey(type), cfg => cfg.UseHandler(type));
                }
            }
        }

        return this;
    }


    /// <summary>
    /// Resolves the routing key of a handler from the <see cref="RoutingKeyAttribute"/> of its
    /// request/query generic argument, falling back to the argument type's full name.
    /// </summary>
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
                throw new NotSupportedException(
                    $"The handler type '{requestHandlerType.FullName}' implements multiple handler interfaces; " +
                    "only one of IRequestHandler<TRequest> or IQueryHandler<TQuery, TResponse> is supported.");
            }

            argType = @interface.GetGenericArguments()[0];
        }

        if (argType == null)
        {
            throw new ArgumentException(
                $"The handler type '{requestHandlerType.FullName}' does not implement " +
                "IRequestHandler<TRequest> or IQueryHandler<TQuery, TResponse>.",
                nameof(requestHandlerType));
        }

        var attribute = argType.GetCustomAttribute<RoutingKeyAttribute>();
        if (attribute != null)
        {
            return attribute.RoutingKey;
        }

        return argType.FullName ?? argType.Name;
    }

    private static bool IsMiddleware(Type type)
    {
        if (!type.IsClass || type.IsAbstract)
        {
            return false;
        }

        return type.GetInterfaces().Any(x => x == typeof(IMiddleware));
    }

    private static bool IsHandler(Type type)
    {
        if (!type.IsClass || type.IsAbstract)
        {
            return false;
        }

        return type.GetInterfaces().Any(x => x == typeof(IHandler));
    }
}