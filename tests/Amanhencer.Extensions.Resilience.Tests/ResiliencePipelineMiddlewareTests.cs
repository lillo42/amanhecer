using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Polly;
using Polly.Registry;
using Polly.Retry;
using Polly.Timeout;

namespace Amanhencer.Extensions.Resilience.Tests;

public class ResiliencePipelineMiddlewareTests
{
    private const string RetryPipeline = "retry";
    private const string TimeoutPipeline = "timeout";
    private const string PassThroughPipeline = "passthrough";

    private static ResiliencePipelineProvider<string> CreateProvider(Action<ResiliencePipelineRegistry<string>>? configure = null)
    {
        var registry = new ResiliencePipelineRegistry<string>();

        registry.TryAddBuilder(RetryPipeline, (builder, _) => builder.AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>(),
            MaxRetryAttempts = 2,
            Delay = TimeSpan.Zero
        }));

        registry.TryAddBuilder(TimeoutPipeline, (builder, _) => builder.AddTimeout(TimeSpan.FromMilliseconds(50)));

        registry.TryAddBuilder(PassThroughPipeline, (_, _) => { });

        configure?.Invoke(registry);
        return registry;
    }

    private static ResiliencePipelineMiddleware CreateMiddleware(
        ResiliencePipelineProvider<string> provider,
        string pipelineName)
    {
        var middleware = new ResiliencePipelineMiddleware(provider);
        middleware.Initialize(pipelineName);
        return middleware;
    }

    private static IPipelineContext CreateContext(
        CancellationToken cancellationToken = default,
        Dictionary<string, object>? metadata = null)
    {
        var context = Substitute.For<IPipelineContext>();
        context.Metadata.Returns(metadata ?? []);
        context.CancellationToken.Returns(cancellationToken);
        context.Request.Returns(new TestRequest("request"));
        context.DeepClone(Arg.Any<Activity?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var clone = Substitute.For<IPipelineContext>();
                clone.CancellationToken.Returns(callInfo.Arg<CancellationToken>());
                return clone;
            });
        return context;
    }

    [Test]
    public async Task When_ExecuteAsync_WhenNextFails_Should_RetryUntilItSucceeds()
    {
        var middleware = CreateMiddleware(CreateProvider(), RetryPipeline);
        var context = CreateContext();
        var next = Substitute.For<Func<IPipelineContext, ValueTask>>();

        var calls = 0;
        next.Invoke(Arg.Any<IPipelineContext>())
            .Returns(_ =>
            {
                calls++;
                if (calls < 3)
                {
                    throw new InvalidOperationException("Boom.");
                }

                return ValueTask.CompletedTask;
            });

        await middleware.ExecuteAsync(context, next);

        await next.Received(3).Invoke(Arg.Any<IPipelineContext>());
    }

    [Test]
    public async Task When_ExecuteAsync_WhenRetriesAreExhausted_Should_RethrowLastException()
    {
        var middleware = CreateMiddleware(CreateProvider(), RetryPipeline);
        var context = CreateContext();
        var next = Substitute.For<Func<IPipelineContext, ValueTask>>();
        next.Invoke(Arg.Any<IPipelineContext>()).Throws(new InvalidOperationException("Boom."));

        await Assert.That(async () => await middleware.ExecuteAsync(context, next))
            .ThrowsExactly<InvalidOperationException>();

        await next.Received(3).Invoke(Arg.Any<IPipelineContext>());
    }

    [Test]
    public async Task When_ExecuteAsync_WhenExceptionIsNotRetryable_Should_NotRetry()
    {
        var middleware = CreateMiddleware(CreateProvider(), RetryPipeline);
        var context = CreateContext();
        var next = Substitute.For<Func<IPipelineContext, ValueTask>>();
        next.Invoke(Arg.Any<IPipelineContext>()).Throws(new ArgumentException("Not handled."));

        await Assert.That(async () => await middleware.ExecuteAsync(context, next))
            .ThrowsExactly<ArgumentException>();

        await next.Received(1).Invoke(Arg.Any<IPipelineContext>());
    }

    [Test]
    public async Task When_ExecuteAsync_WhenExecutionExceedsTimeout_Should_ThrowTimeoutRejected()
    {
        var middleware = CreateMiddleware(CreateProvider(), TimeoutPipeline);
        var context = CreateContext();
        var next = Substitute.For<Func<IPipelineContext, ValueTask>>();
        next.Invoke(Arg.Any<IPipelineContext>())
            .Returns(callInfo => new ValueTask(
                Task.Delay(TimeSpan.FromSeconds(10), callInfo.Arg<IPipelineContext>().CancellationToken)));

        await Assert.That(async () => await middleware.ExecuteAsync(context, next))
            .ThrowsExactly<TimeoutRejectedException>();
    }

    [Test]
    public async Task When_ExecuteAsync_WhenTokenIsCancelled_Should_NotCallNext()
    {
        var middleware = CreateMiddleware(CreateProvider(), RetryPipeline);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        var context = CreateContext(cancellationTokenSource.Token);
        var next = Substitute.For<Func<IPipelineContext, ValueTask>>();

        await Assert.That(async () => await middleware.ExecuteAsync(context, next))
            .ThrowsExactly<OperationCanceledException>();

        await next.DidNotReceive().Invoke(Arg.Any<IPipelineContext>());
    }

    [Test]
    public async Task When_ExecuteAsync_Should_PassCallerCancellationTokenToNext()
    {
        var middleware = CreateMiddleware(CreateProvider(), PassThroughPipeline);
        using var cancellationTokenSource = new CancellationTokenSource();
        var context = CreateContext(cancellationTokenSource.Token);
        var next = Substitute.For<Func<IPipelineContext, ValueTask>>();

        IPipelineContext? seenByNext = null;
        next.Invoke(Arg.Any<IPipelineContext>())
            .Returns(callInfo =>
            {
                seenByNext = callInfo.Arg<IPipelineContext>();
                return ValueTask.CompletedTask;
            });

        await middleware.ExecuteAsync(context, next);

        await Assert.That(seenByNext).IsNotNull();
        await Assert.That(seenByNext!.CancellationToken).IsEqualTo(cancellationTokenSource.Token);
    }

    [Test]
    public async Task When_ExecuteAsync_WhenResilienceContextIsProvidedInMetadata_Should_UseIt()
    {
        var middleware = CreateMiddleware(CreateProvider(), PassThroughPipeline);
        using var cancellationTokenSource = new CancellationTokenSource();
        var providedContext = ResilienceContextPool.Shared.Get(cancellationTokenSource.Token);
        try
        {
            var context = CreateContext(metadata: new Dictionary<string, object>
            {
                [ResiliencePipelineMiddleware.ResilienceContextKey] = providedContext
            });
            var next = Substitute.For<Func<IPipelineContext, ValueTask>>();

            IPipelineContext? seenByNext = null;
            next.Invoke(Arg.Any<IPipelineContext>())
                .Returns(callInfo =>
                {
                    seenByNext = callInfo.Arg<IPipelineContext>();
                    return ValueTask.CompletedTask;
                });

            await middleware.ExecuteAsync(context, next);

            await Assert.That(seenByNext).IsNotNull();
            await Assert.That(seenByNext!.CancellationToken).IsEqualTo(cancellationTokenSource.Token);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(providedContext);
        }
    }

    [Test]
    public async Task When_ExecuteAsync_Should_EnrichResilienceContextWithRequestMetadata()
    {
        // The middleware returns the rented ResilienceContext to the pool when execution
        // completes, which clears its properties — so the metadata must be observed during
        // execution, not after.
        string? capturedRequestName = null;
        var provider = CreateProvider(registry => registry.TryAddBuilder("capturing", (builder, _) =>
            builder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>(),
                MaxRetryAttempts = 1,
                Delay = TimeSpan.Zero,
                OnRetry = args =>
                {
                    capturedRequestName = args.Context.GetRequestMetadata()?.RequestName;
                    return ValueTask.CompletedTask;
                }
            })));
        var middleware = CreateMiddleware(provider, "capturing");
        var context = CreateContext();
        var next = Substitute.For<Func<IPipelineContext, ValueTask>>();

        var calls = 0;
        next.Invoke(Arg.Any<IPipelineContext>())
            .Returns(_ =>
            {
                calls++;
                if (calls == 1)
                {
                    throw new InvalidOperationException("Boom.");
                }

                return ValueTask.CompletedTask;
            });

        await middleware.ExecuteAsync(context, next);

        await Assert.That(capturedRequestName).IsEqualTo(nameof(TestRequest));
    }

    [Test]
    public async Task When_ExecuteAsync_WhenRequestMetadataIsProvided_Should_NotOverwriteIt()
    {
        ResilienceContext? capturedContext = null;
        var provider = CreateProvider(registry => registry.TryAddBuilder("capturing", (builder, _) =>
            builder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>(),
                MaxRetryAttempts = 1,
                Delay = TimeSpan.Zero,
                OnRetry = args =>
                {
                    capturedContext = args.Context;
                    return ValueTask.CompletedTask;
                }
            })));
        var middleware = CreateMiddleware(provider, "capturing");
        var providedContext = ResilienceContextPool.Shared.Get();
        providedContext.SetRequestMetadata(new RequestMetadata { RequestName = "caller.name" });
        try
        {
            var context = CreateContext(metadata: new Dictionary<string, object>
            {
                [ResiliencePipelineMiddleware.ResilienceContextKey] = providedContext
            });
            var next = Substitute.For<Func<IPipelineContext, ValueTask>>();

            var calls = 0;
            next.Invoke(Arg.Any<IPipelineContext>())
                .Returns(_ =>
                {
                    calls++;
                    if (calls == 1)
                    {
                        throw new InvalidOperationException("Boom.");
                    }

                    return ValueTask.CompletedTask;
                });

            await middleware.ExecuteAsync(context, next);

            await Assert.That(capturedContext?.GetRequestMetadata()?.RequestName).IsEqualTo("caller.name");
        }
        finally
        {
            ResilienceContextPool.Shared.Return(providedContext);
        }
    }

    [Test]
    public async Task When_ExecuteAsync_WhenNotInitialized_Should_ThrowInvalidOperation()
    {
        var middleware = new ResiliencePipelineMiddleware(CreateProvider());
        var context = CreateContext();
        var next = Substitute.For<Func<IPipelineContext, ValueTask>>();

        await Assert.That(async () => await middleware.ExecuteAsync(context, next))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task When_ExecuteAsync_WhenPipelineNameIsUnknown_Should_ThrowKeyNotFound()
    {
        var middleware = CreateMiddleware(CreateProvider(), "unknown");
        var context = CreateContext();
        var next = Substitute.For<Func<IPipelineContext, ValueTask>>();

        await Assert.That(async () => await middleware.ExecuteAsync(context, next))
            .ThrowsExactly<KeyNotFoundException>();
    }

    [Test]
    public async Task When_Initialize_WhenMetadataIsAttribute_Should_ExecuteNamedPipeline()
    {
        var middleware = new ResiliencePipelineMiddleware(CreateProvider());
        middleware.Initialize(new ResiliencePipelineAttribute(RetryPipeline, 1));
        var context = CreateContext();
        var next = Substitute.For<Func<IPipelineContext, ValueTask>>();

        var calls = 0;
        next.Invoke(Arg.Any<IPipelineContext>())
            .Returns(_ =>
            {
                calls++;
                if (calls == 1)
                {
                    throw new InvalidOperationException("Boom.");
                }

                return ValueTask.CompletedTask;
            });

        await middleware.ExecuteAsync(context, next);

        await next.Received(2).Invoke(Arg.Any<IPipelineContext>());
    }

    [Test]
    public async Task When_SendAsync_WhenHandlerHasAttribute_Should_RetryViaNamedPipelineFromDI()
    {
        var state = new FlakyRequestState();
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(state);
        services.AddResiliencePipeline(RetryPipeline, builder => builder.AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>(),
            MaxRetryAttempts = 2,
            Delay = TimeSpan.Zero
        }));
        services.AddAmanhencer(configurator => configurator.AddRequestHandler<FlakyRequestHandler>());
        var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IDispatcher>().SendAsync(new TestRequest("request"));

        await Assert.That(state.Calls).IsEqualTo(2);
    }

    [Test]
    public async Task When_Initialize_WhenMetadataIsInvalid_Should_ThrowArgumentException()
    {
        var middleware = new ResiliencePipelineMiddleware(CreateProvider());

        await Assert.That(() => middleware.Initialize(null)).ThrowsExactly<ArgumentException>();
        await Assert.That(() => middleware.Initialize("")).ThrowsExactly<ArgumentException>();
        await Assert.That(() => middleware.Initialize(42)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_AttributeIsCreated_Should_ReturnMiddlewareTypeAndKeepOrderAndName()
    {
        var attribute = new ResiliencePipelineAttribute(RetryPipeline, 5);

        await Assert.That(attribute.GetMiddlewareType()).IsEqualTo(typeof(ResiliencePipelineMiddleware));
        await Assert.That(attribute.Order).IsEqualTo(5);
        await Assert.That(attribute.PipelineName).IsEqualTo(RetryPipeline);
    }

    public sealed record TestRequest(string Value);

    public sealed class FlakyRequestState
    {
        public int Calls;
    }

    [ResiliencePipeline(RetryPipeline, 1)]
    public sealed class FlakyRequestHandler(FlakyRequestState state) : RequestHandler<TestRequest>
    {
        public override ValueTask HandleAsync(
            TestRequest request,
            IPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref state.Calls) == 1)
            {
                throw new InvalidOperationException("Boom.");
            }

            return ValueTask.CompletedTask;
        }
    }
}
