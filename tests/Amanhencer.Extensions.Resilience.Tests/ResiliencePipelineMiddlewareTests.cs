using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Diagnostics;
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

    [Test]
    public async Task Execute_RetriesOnFailure_UntilNextSucceeds()
    {
        var middleware = CreateMiddleware(CreateProvider(), RetryPipeline);
        var calls = 0;

        await middleware.ExecuteAsync(TestPipelineContext.Create(), _ =>
        {
            calls++;
            if (calls < 3)
            {
                throw new InvalidOperationException("Boom.");
            }

            return ValueTask.CompletedTask;
        });

        await Assert.That(calls).IsEqualTo(3);
    }

    [Test]
    public async Task Execute_RetryExhausted_RethrowsLastException()
    {
        var middleware = CreateMiddleware(CreateProvider(), RetryPipeline);
        var calls = 0;

        await Assert.That(async () => await middleware.ExecuteAsync(TestPipelineContext.Create(), _ =>
            {
                calls++;
                throw new InvalidOperationException("Boom.");
            }))
            .ThrowsExactly<InvalidOperationException>();

        await Assert.That(calls).IsEqualTo(3);
    }

    [Test]
    public async Task Execute_NonRetryableException_DoesNotRetry()
    {
        var middleware = CreateMiddleware(CreateProvider(), RetryPipeline);
        var calls = 0;

        await Assert.That(async () => await middleware.ExecuteAsync(TestPipelineContext.Create(), _ =>
            {
                calls++;
                throw new ArgumentException("Not handled.");
            }))
            .ThrowsExactly<ArgumentException>();

        await Assert.That(calls).IsEqualTo(1);
    }

    [Test]
    public async Task Execute_TimeoutExceeded_ThrowsTimeoutRejected()
    {
        var middleware = CreateMiddleware(CreateProvider(), TimeoutPipeline);

        await Assert.That(async () => await middleware.ExecuteAsync(TestPipelineContext.Create(), async context =>
                await Task.Delay(TimeSpan.FromSeconds(10), context.CancellationToken)))
            .ThrowsExactly<TimeoutRejectedException>();
    }

    [Test]
    public async Task Execute_WithCancelledToken_DoesNotCallNext()
    {
        var middleware = CreateMiddleware(CreateProvider(), RetryPipeline);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var calls = 0;

        await Assert.That(async () => await middleware.ExecuteAsync(
                TestPipelineContext.Create(cancellationToken: cts.Token),
                _ =>
                {
                    calls++;
                    return ValueTask.CompletedTask;
                }))
            .ThrowsExactly<OperationCanceledException>();

        await Assert.That(calls).IsEqualTo(0);
    }

    [Test]
    public async Task Execute_PassesCallerCancellationTokenToNext()
    {
        var middleware = CreateMiddleware(CreateProvider(), PassThroughPipeline);
        using var cts = new CancellationTokenSource();
        CancellationToken seenByNext = default;

        await middleware.ExecuteAsync(TestPipelineContext.Create(cancellationToken: cts.Token), context =>
        {
            seenByNext = context.CancellationToken;
            return ValueTask.CompletedTask;
        });

        await Assert.That(seenByNext).IsEqualTo(cts.Token);
    }

    [Test]
    public async Task Execute_UsesProvidedResilienceContext_FromMetadata()
    {
        var middleware = CreateMiddleware(CreateProvider(), PassThroughPipeline);
        using var cts = new CancellationTokenSource();
        var providedContext = ResilienceContextPool.Shared.Get(cts.Token);
        try
        {
            var context = TestPipelineContext.Create(metadata: new Dictionary<string, object>
            {
                [ResiliencePipelineMiddleware.ResilienceContextKey] = providedContext
            });
            CancellationToken seenByNext = default;

            await middleware.ExecuteAsync(context, pipelineContext =>
            {
                seenByNext = pipelineContext.CancellationToken;
                return ValueTask.CompletedTask;
            });

            await Assert.That(seenByNext).IsEqualTo(cts.Token);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(providedContext);
        }
    }

    [Test]
    public async Task Execute_EnrichesResilienceContext_WithRequestMetadata()
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
        var calls = 0;

        await middleware.ExecuteAsync(TestPipelineContext.Create(), _ =>
        {
            calls++;
            if (calls == 1)
            {
                throw new InvalidOperationException("Boom.");
            }

            return ValueTask.CompletedTask;
        });

        await Assert.That(capturedRequestName).IsEqualTo(nameof(TestRequest));
    }

    [Test]
    public async Task Execute_DoesNotOverwriteCallerProvidedRequestMetadata()
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
            var calls = 0;
            await middleware.ExecuteAsync(
                TestPipelineContext.Create(metadata: new Dictionary<string, object>
                {
                    [ResiliencePipelineMiddleware.ResilienceContextKey] = providedContext
                }),
                _ =>
                {
                    calls++;
                    if (calls == 1)
                    {
                        throw new InvalidOperationException("Boom.");
                    }

                    return ValueTask.CompletedTask;
                });

            await Assert.That(capturedContext?.GetRequestMetadata()?.RequestName).IsEqualTo("caller.name");
        }
        finally
        {
            ResilienceContextPool.Shared.Return(providedContext);
        }
    }

    [Test]
    public async Task Execute_WithoutInitialize_ThrowsInvalidOperation()
    {
        var middleware = new ResiliencePipelineMiddleware(CreateProvider());

        await Assert.That(async () => await middleware.ExecuteAsync(
                TestPipelineContext.Create(), _ => ValueTask.CompletedTask))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Execute_UnknownPipelineName_ThrowsKeyNotFound()
    {
        var middleware = CreateMiddleware(CreateProvider(), "unknown");

        await Assert.That(async () => await middleware.ExecuteAsync(
                TestPipelineContext.Create(), _ => ValueTask.CompletedTask))
            .ThrowsExactly<KeyNotFoundException>();
    }

    [Test]
    public async Task Initialize_AttributeMetadata_ExecutesNamedPipeline()
    {
        var middleware = new ResiliencePipelineMiddleware(CreateProvider());
        middleware.Initialize(new ResiliencePipelineAttribute(RetryPipeline, 1));
        var calls = 0;

        await middleware.ExecuteAsync(TestPipelineContext.Create(), _ =>
        {
            calls++;
            if (calls == 1)
            {
                throw new InvalidOperationException("Boom.");
            }

            return ValueTask.CompletedTask;
        });

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task SendAsync_AttributeOnHandler_RetriesViaNamedPipelineFromDI()
    {
        var state = new FlakyRequestState();
        var services = new ServiceCollection();
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
    public async Task Initialize_InvalidMetadata_ThrowsArgumentException()
    {
        var middleware = new ResiliencePipelineMiddleware(CreateProvider());

        await Assert.That(() => middleware.Initialize(null)).ThrowsExactly<ArgumentException>();
        await Assert.That(() => middleware.Initialize("")).ThrowsExactly<ArgumentException>();
        await Assert.That(() => middleware.Initialize(42)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Attribute_ReturnsMiddlewareType_AndKeepsOrderAndName()
    {
        var attribute = new ResiliencePipelineAttribute(RetryPipeline, 5);

        await Assert.That(attribute.GetMiddlewareType()).IsEqualTo(typeof(ResiliencePipelineMiddleware));
        await Assert.That(attribute.Order).IsEqualTo(5);
        await Assert.That(attribute.PipelineName).IsEqualTo(RetryPipeline);
    }
}
