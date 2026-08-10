using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;
using Polly.Retry;
using Polly.Timeout;

namespace Amanhencer.Polly.Tests;

public class PollyResiliencePipelineMiddlewareTests
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

    private static PollyResiliencePipelineMiddleware CreateMiddleware(
        ResiliencePipelineProvider<string> provider,
        string pipelineName)
    {
        var middleware = new PollyResiliencePipelineMiddleware(provider);
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
                [PollyResiliencePipelineMiddleware.ResilienceContext] = providedContext
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
    public async Task Execute_WithoutInitialize_ThrowsInvalidOperation()
    {
        var middleware = new PollyResiliencePipelineMiddleware(CreateProvider());

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
        var middleware = new PollyResiliencePipelineMiddleware(CreateProvider());
        middleware.Initialize(new PollyResiliencePipelineAttribute(RetryPipeline, 1));
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
        services.AddSingleton<ResiliencePipelineProvider<string>>(CreateProvider());
        services.AddAmanhencer(configurator => configurator.AddRequestHandler<FlakyRequestHandler>());
        var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IDispatcher>().SendAsync(new TestRequest("request"));

        await Assert.That(state.Calls).IsEqualTo(2);
    }

    [Test]
    public async Task Initialize_InvalidMetadata_ThrowsArgumentException()
    {
        var middleware = new PollyResiliencePipelineMiddleware(CreateProvider());

        await Assert.That(() => middleware.Initialize(null)).ThrowsExactly<ArgumentException>();
        await Assert.That(() => middleware.Initialize("")).ThrowsExactly<ArgumentException>();
        await Assert.That(() => middleware.Initialize(42)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Attribute_ReturnsMiddlewareType_AndKeepsOrderAndName()
    {
        var attribute = new PollyResiliencePipelineAttribute(RetryPipeline, 5);

        await Assert.That(attribute.GetMiddlewareType()).IsEqualTo(typeof(PollyResiliencePipelineMiddleware));
        await Assert.That(attribute.Order).IsEqualTo(5);
        await Assert.That(attribute.PipelineName).IsEqualTo(RetryPipeline);
    }
}
