using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Amanhecer.Configurator;

/// <summary>
/// Configures Amanhecer: registers handlers, routing keys and the executing strategy
/// into the service collection.
/// </summary>
/// <param name="services">The service collection where handlers and middlewares are registered.</param>
public class AmanhecerConfigurator(IServiceCollection services)
{
    private readonly HashSet<Type> _autoRegisteredTypes = [];

    /// <summary>
    /// Gets the routing options built from the configured routing keys.
    /// </summary>
    public List<AmanhecerRoutingOptions> RoutingConfigurators { get; } = [];

    public List<IGateway> Gateways { get; set; } = [];

    public Dictionary<string, IReadOnlyList<AmanhecerTransformerOptions>> TransformerPipelineConfiguration { get; } =
        [];

    /// <summary>
    /// Gets the transformers applied to every encode and decode transformer pipeline.
    /// </summary>
    public List<AmanhecerTransformerOptions> GlobalTransformers { get; } = [];


    /// <summary>
    /// Registers a request handler and creates a pipeline for the routing key derived from its request type.
    /// </summary>
    /// <typeparam name="TRequestHandler">The request handler type to register.</typeparam>
    /// <param name="configure">An optional action to configure the pipeline's middlewares.</param>
    /// <returns>The current <see cref="AmanhecerConfigurator"/>, for chaining.</returns>
    public AmanhecerConfigurator AddRequestHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors |
                                    DynamicallyAccessedMemberTypes.Interfaces |
                                    DynamicallyAccessedMemberTypes.PublicMethods)]
        TRequestHandler>(
        Action<AmanhecerRoutingConfigurator>? configure = null)
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
    /// <returns>The current <see cref="AmanhecerConfigurator"/>, for chaining.</returns>
    public AmanhecerConfigurator AddQueryHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors |
                                    DynamicallyAccessedMemberTypes.Interfaces |
                                    DynamicallyAccessedMemberTypes.PublicMethods)]
        TQueryHandler>(
        Action<AmanhecerRoutingConfigurator>? configure = null)
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
    /// <returns>The current <see cref="AmanhecerConfigurator"/>, for chaining.</returns>
    public AmanhecerConfigurator AddRoutingKey(string routingKey,
        Action<AmanhecerRoutingConfigurator>? configure = null)
    {
        var cfg = new AmanhecerRoutingConfigurator(routingKey, services);

        configure?.Invoke(cfg);

        RoutingConfigurators.Add(cfg.ToOptions());
        return this;
    }

    /// <summary>
    /// Registers the default <see cref="IExecutingStrategy"/> used when a context does not specify one.
    /// </summary>
    /// <param name="executor">The executing strategy to register as a singleton.</param>
    /// <returns>The current <see cref="AmanhecerConfigurator"/>, for chaining.</returns>
    public AmanhecerConfigurator SetExecutorStrategy(IExecutingStrategy executor)
    {
        services.AddSingleton(executor);
        return this;
    }

    public AmanhecerConfigurator UsingMessagingGateway(Action<AmanhecerMessagingConfigurator> configure)
    {
        var cfg = new AmanhecerMessagingConfigurator(services);
        configure.Invoke(cfg);
        Gateways.AddRange(cfg.Gateways);

        if (HasDuplicated(Gateways))
        {
            throw new NotImplementedException();
        }

        foreach (var keyPairValue in cfg.TransformerPipeline)
        {
            if (TransformerPipelineConfiguration.ContainsKey(keyPairValue.Key))
            {
                throw new NotImplementedException();
            }

            TransformerPipelineConfiguration[keyPairValue.Key] = keyPairValue.Value;
        }

        GlobalTransformers.AddRange(cfg.GlobalTransformers);

        return this;

        static bool HasDuplicated(List<IGateway> gateways)
        {
            var hash = new HashSet<string>();
            foreach (var gateway in gateways)
            {
                if (!hash.Add(gateway.Name))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Scans assemblies for concrete middleware and handler types and registers them: middlewares
    /// as transient services and handlers in a pipeline keyed by their request/query type.
    /// Open generic types and types that fail to load are skipped, and each type is registered at
    /// most once, even when the same assembly is scanned multiple times.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan. When empty, the calling assembly is scanned.</param>
    /// <returns>The current <see cref="AmanhecerConfigurator"/>, for chaining.</returns>
    [RequiresUnreferencedCode(
        "Assembly scanning requires all handler and middleware types to be preserved; " +
        "prefer explicit registration in trimmed or AOT-compiled applications.")]
    [UnconditionalSuppressMessage("Trimming", "IL2072",
        Justification = "Types returned by Assembly.GetTypes() cannot carry annotations; " +
                        "callers are warned via RequiresUnreferencedCode.")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public AmanhecerConfigurator AutoFromAssemblies(params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        foreach (var assembly in assemblies)
        {
            foreach (var type in GetLoadableTypes(assembly))
            {
                if (!_autoRegisteredTypes.Add(type))
                {
                    continue;
                }

                if (IsMiddleware(type) || IsTransformer(type) || IsMessageMapper(type))
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

    [RequiresUnreferencedCode(
        "Assembly scanning requires all handler and middleware types to be preserved; " +
        "prefer explicit registration in trimmed or AOT-compiled applications.")]
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null)!;
        }
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

    private static bool IsMiddleware(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
        Type type)
    {
        if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters)
        {
            return false;
        }

        return type.GetInterfaces().Any(x => x == typeof(IMiddleware));
    }

    private static bool IsHandler(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
        Type type)
    {
        if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters)
        {
            return false;
        }

        return type.GetInterfaces().Any(x => x == typeof(IHandler));
    }

    private static bool IsTransformer(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
        Type type)
    {
        if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters)
        {
            return false;
        }

        return type.GetInterfaces().Any(x => x == typeof(IEncodeTransformer) || x == typeof(IDecodeTransformer));
    }

    private static bool IsMessageMapper(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
        Type type)
    {
        if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters)
        {
            return false;
        }

        return type.GetInterfaces().Any(x => x == typeof(IMessageMapper));
    }
}