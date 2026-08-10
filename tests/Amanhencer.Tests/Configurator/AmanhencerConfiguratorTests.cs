using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Configurator;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhencer.Tests.Configurator;

public class AmanhencerConfiguratorTests
{
    private const string DynamicAssemblyName = "Amanhencer.Tests.DynamicDoubles";

    [Test]
    [RequiresUnreferencedCode(
        "Assembly scanning requires all handler and middleware types to be preserved; " +
        "prefer explicit registration in trimmed or AOT-compiled applications.")]
    [RequiresDynamicCode("Uses Reflection.Emit to build test doubles.")]
    public async Task When_AutoFromAssemblies_Should_RegisterMiddlewares()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhencerConfigurator(services);
        var assembly = CreateDynamicAssembly();

        configurator.AutoFromAssemblies(assembly);

        var middlewareType = assembly.GetType("DynamicMiddleware")!;
        var descriptor = services.SingleOrDefault(x => x.ServiceType == middlewareType);
        await Assert.That(descriptor).IsNotNull()
            .And.Member(x => x.Lifetime, y => y.IsEqualTo(ServiceLifetime.Transient))
            .And.Member(x => x.ImplementationType, y => y.IsEqualTo(middlewareType));
    }

    [Test]
    [RequiresUnreferencedCode(
        "Assembly scanning requires all handler and middleware types to be preserved; " +
        "prefer explicit registration in trimmed or AOT-compiled applications.")]
    [RequiresDynamicCode("Uses Reflection.Emit to build test doubles.")]
    public async Task When_AutoFromAssemblies_Should_RegisterHandlers()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhencerConfigurator(services);
        var assembly = CreateDynamicAssembly();

        configurator.AutoFromAssemblies(assembly);

        var requestHandlerType = assembly.GetType("DynamicRequestHandler")!;
        var queryHandlerType = assembly.GetType("DynamicQueryHandler")!;

        await Assert.That(services)
            .Contains(x => x.ServiceType == requestHandlerType && x.Lifetime == ServiceLifetime.Transient)
            .And.Contains(x => x.ServiceType == queryHandlerType && x.Lifetime == ServiceLifetime.Transient);

        var routingKeys = configurator.RoutingConfigurators
            .Select(x => x.RoutingKey)
            .ToArray();

        await Assert.That(routingKeys)
            .Contains("DynamicRequest")
            .And.Contains("dynamic.query");
    }

    [Test]
    [RequiresUnreferencedCode(
        "Assembly scanning requires all handler and middleware types to be preserved; " +
        "prefer explicit registration in trimmed or AOT-compiled applications.")]
    [RequiresDynamicCode("Uses Reflection.Emit to build test doubles.")]
    public async Task When_AutoFromAssemblies_Should_SkipNonConcreteTypes()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhencerConfigurator(services);
        var assembly = CreateDynamicAssembly();

        configurator.AutoFromAssemblies(assembly);

        await Assert.That(services)
            .DoesNotContain(x => x.ServiceType == assembly.GetType("DynamicAbstractMiddleware"))
            .And.DoesNotContain(x => x.ServiceType == assembly.GetType("DynamicOpenGenericHandler`1"))
            .And.DoesNotContain(x => x.ServiceType == assembly.GetType("IDynamicMiddlewareContract"))
            // Only the concrete middleware, both handlers and the terminal ExecuteHandlerMiddleware
            // appended per pipeline should be registered.
            .And.Count().IsEqualTo(4);
    }

    [Test]
    [RequiresUnreferencedCode(
        "Assembly scanning requires all handler and middleware types to be preserved; " +
        "prefer explicit registration in trimmed or AOT-compiled applications.")]
    [RequiresDynamicCode("Uses Reflection.Emit to build test doubles.")]
    public async Task When_AutoFromAssemblies_CalledTwice_Should_RegisterTypesOnce()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhencerConfigurator(services);
        var assembly = CreateDynamicAssembly();

        configurator.AutoFromAssemblies(assembly);
        configurator.AutoFromAssemblies(assembly);

        await Assert.That(services).Count().IsEqualTo(4);
        await Assert.That(configurator.RoutingConfigurators).Count().IsEqualTo(2);
    }

    [Test]
    [RequiresUnreferencedCode(
        "Assembly scanning requires all handler and middleware types to be preserved; " +
        "prefer explicit registration in trimmed or AOT-compiled applications.")]
    public async Task When_AutoFromAssemblies_WithoutAssemblies_Should_ScanCallingAssembly()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhencerConfigurator(services);

        // The calling assembly (this test assembly) contains handlers that only implement the
        // non-generic marker interface, so scanning it must fail while deriving their routing
        // key — proving the calling assembly was the one scanned.
        await Assert.That(() => configurator.AutoFromAssemblies())
            .Throws<Exception>()
            .WithMessageContaining("Amanhencer.Tests");
    }

    [Test]
    public async Task When_AddRequestHandler_WithMultipleHandlerInterfaces_Should_ThrowNotSupportedException()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhencerConfigurator(services);

        await Assert.That(() => configurator.AddRequestHandler<MultiInterfacesHandler>())
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task When_AddRequestHandler_WithOnlyMarkerInterface_Should_ThrowArgumentException()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhencerConfigurator(services);

        await Assert.That(() => configurator.AddRequestHandler<MarkerOnlyRequestHandler>())
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task When_AddRequestHandler_WithRoutingKeyAttribute_Should_UseAttributeValue()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhencerConfigurator(services);

        configurator.AddRequestHandler<AttributedRequestHandler>();

        var routingKey = configurator.RoutingConfigurators.Single().RoutingKey;
        await Assert.That(routingKey).IsEqualTo("tests.attributed-request");
    }

    [Test]
    public async Task When_AddRequestHandler_WithoutRoutingKeyAttribute_Should_UseRequestFullName()
    {
        var services = new ServiceCollection();
        var configurator = new AmanhencerConfigurator(services);

        configurator.AddRequestHandler<PlainRequestHandler>();

        var routingKey = configurator.RoutingConfigurators.Single().RoutingKey;
        await Assert.That(routingKey).IsEqualTo(typeof(PlainRequest).FullName);
    }

    [RequiresDynamicCode("Uses Reflection.Emit to build test doubles.")]
    private static Assembly CreateDynamicAssembly()
    {
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(DynamicAssemblyName), AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule(DynamicAssemblyName);

        var requestType = moduleBuilder
            .DefineType("DynamicRequest", TypeAttributes.Public | TypeAttributes.Class)
            .CreateType();

        var requestHandlerType = moduleBuilder.DefineType(
            "DynamicRequestHandler", TypeAttributes.Public | TypeAttributes.Class);
        requestHandlerType.AddInterfaceImplementation(typeof(IRequestHandler<>).MakeGenericType(requestType));
        AddValueTaskMethod(requestHandlerType, "HandleAsync", typeof(ValueTask),
            requestType, typeof(IPipelineContext), typeof(CancellationToken));
        requestHandlerType.CreateType();

        var queryTypeBuilder = moduleBuilder.DefineType(
            "DynamicQuery", TypeAttributes.Public | TypeAttributes.Class);
        queryTypeBuilder.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(RoutingKeyAttribute).GetConstructor([typeof(string)])!, ["dynamic.query"]));
        var queryType = queryTypeBuilder.CreateType();

        var queryHandlerType = moduleBuilder.DefineType(
            "DynamicQueryHandler", TypeAttributes.Public | TypeAttributes.Class);
        queryHandlerType.AddInterfaceImplementation(
            typeof(IQueryHandler<,>).MakeGenericType(queryType, typeof(string)));
        AddValueTaskMethod(queryHandlerType, "HandleAsync", typeof(ValueTask<string>),
            queryType, typeof(IPipelineContext), typeof(CancellationToken));
        queryHandlerType.CreateType();

        var middlewareType = moduleBuilder.DefineType(
            "DynamicMiddleware", TypeAttributes.Public | TypeAttributes.Class);
        middlewareType.AddInterfaceImplementation(typeof(IMiddleware));
        AddValueTaskMethod(middlewareType, "ExecuteAsync", typeof(ValueTask),
            typeof(IPipelineContext), typeof(Func<IPipelineContext, ValueTask>));
        AddVoidMethod(middlewareType, "Initialize", typeof(object));
        middlewareType.CreateType();

        var abstractMiddlewareType = moduleBuilder.DefineType(
            "DynamicAbstractMiddleware",
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract);
        abstractMiddlewareType.AddInterfaceImplementation(typeof(IMiddleware));
        abstractMiddlewareType.CreateType();

        var openGenericHandlerType = moduleBuilder.DefineType(
            "DynamicOpenGenericHandler`1", TypeAttributes.Public | TypeAttributes.Class);
        var genericParameters = openGenericHandlerType.DefineGenericParameters("T");
        openGenericHandlerType.AddInterfaceImplementation(
            typeof(IRequestHandler<>).MakeGenericType(genericParameters[0]));
        AddValueTaskMethod(openGenericHandlerType, "HandleAsync", typeof(ValueTask),
            genericParameters[0], typeof(IPipelineContext), typeof(CancellationToken));
        openGenericHandlerType.CreateType();

        var subInterfaceType = moduleBuilder.DefineType(
            "IDynamicMiddlewareContract",
            TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        subInterfaceType.AddInterfaceImplementation(typeof(IMiddleware));
        subInterfaceType.CreateType();

        return assemblyBuilder;
    }

    private static void AddValueTaskMethod(TypeBuilder typeBuilder, string name, Type returnType,
        params Type[] parameterTypes)
    {
        var methodBuilder = typeBuilder.DefineMethod(name,
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final |
            MethodAttributes.HideBySig | MethodAttributes.NewSlot,
            returnType, parameterTypes);

        var il = methodBuilder.GetILGenerator();
        var local = il.DeclareLocal(returnType);
        il.Emit(OpCodes.Ldloca_S, local);
        il.Emit(OpCodes.Initobj, returnType);
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ret);
    }

    private static void AddVoidMethod(TypeBuilder typeBuilder, string name, params Type[] parameterTypes)
    {
        var methodBuilder = typeBuilder.DefineMethod(name,
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final |
            MethodAttributes.HideBySig | MethodAttributes.NewSlot,
            typeof(void), parameterTypes);

        methodBuilder.GetILGenerator().Emit(OpCodes.Ret);
    }

    private record PlainRequest;

    private class PlainRequestHandler : RequestHandler<PlainRequest>
    {
        public override ValueTask HandleAsync(PlainRequest request, IPipelineContext context,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    [RoutingKey("tests.attributed-request")]
    private record AttributedRequest;

    private class AttributedRequestHandler : RequestHandler<AttributedRequest>
    {
        public override ValueTask HandleAsync(AttributedRequest request, IPipelineContext context,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private record MultiRequest;

    private record MultiQuery;

    private class MultiInterfacesHandler : IRequestHandler<MultiRequest>, IQueryHandler<MultiQuery, string>
    {
        public ValueTask HandleAsync(MultiRequest request, IPipelineContext context,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<string> HandleAsync(MultiQuery query, IPipelineContext context,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(string.Empty);
    }

    private class MarkerOnlyRequestHandler : IRequestHandler
    {
        public ValueTask HandleAsync(object request, IPipelineContext context,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}