using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Polly;
using Polly.Registry;
using Polly.Retry;
using Polly.Timeout;

namespace Amanhecer.Polly.Tests;

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

    private static AmanhecerContext CreateContext(
        CancellationToken cancellationToken = default,
        Dictionary<string, object>? metadata = null)
    {
        var context = Substitute.For<AmanhecerContext>();
        context.Metadata.Returns(metadata ?? []);
        context.CancellationToken.Returns(cancellationToken);
        context.Request.Returns(new TestRequest("request"));
        context.DeepClone(Arg.Any<Activity?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var clone = Substitute.For<AmanhecerContext>();
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
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        var calls = 0;
        next.Invoke(Arg.Any<AmanhecerContext>())
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

        await next.Received(3).Invoke(Arg.Any<AmanhecerContext>());
    }

    [Test]
    public async Task When_ExecuteAsync_WhenRetriesAreExhausted_Should_RethrowLastException()
    {
        var middleware = CreateMiddleware(CreateProvider(), RetryPipeline);
        var context = CreateContext();
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();
        next.Invoke(Arg.Any<AmanhecerContext>()).Throws(new InvalidOperationException("Boom."));

        await Assert.That(async () => await middleware.ExecuteAsync(context, next))
            .ThrowsExactly<InvalidOperationException>();

        await next.Received(3).Invoke(Arg.Any<AmanhecerContext>());
    }

    [Test]
    public async Task When_ExecuteAsync_WhenExceptionIsNotRetryable_Should_NotRetry()
    {
        var middleware = CreateMiddleware(CreateProvider(), RetryPipeline);
        var context = CreateContext();
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();
        next.Invoke(Arg.Any<AmanhecerContext>()).Throws(new ArgumentException("Not handled."));

        await Assert.That(async () => await middleware.ExecuteAsync(context, next))
            .ThrowsExactly<ArgumentException>();

        await next.Received(1).Invoke(Arg.Any<AmanhecerContext>());
    }

    [Test]
    public async Task When_ExecuteAsync_WhenExecutionExceedsTimeout_Should_ThrowTimeoutRejected()
    {
        var middleware = CreateMiddleware(CreateProvider(), TimeoutPipeline);
        var context = CreateContext();
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();
        next.Invoke(Arg.Any<AmanhecerContext>())
            .Returns(callInfo => new ValueTask(
                Task.Delay(TimeSpan.FromSeconds(10), callInfo.Arg<AmanhecerContext>().CancellationToken)));

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
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        await Assert.That(async () => await middleware.ExecuteAsync(context, next))
            .ThrowsExactly<OperationCanceledException>();

        await next.DidNotReceive().Invoke(Arg.Any<AmanhecerContext>());
    }

    [Test]
    public async Task When_ExecuteAsync_Should_PassCallerCancellationTokenToNext()
    {
        var middleware = CreateMiddleware(CreateProvider(), PassThroughPipeline);
        using var cancellationTokenSource = new CancellationTokenSource();
        var context = CreateContext(cancellationTokenSource.Token);
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        AmanhecerContext? seenByNext = null;
        next.Invoke(Arg.Any<AmanhecerContext>())
            .Returns(callInfo =>
            {
                seenByNext = callInfo.Arg<AmanhecerContext>();
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
                [PollyResiliencePipelineMiddleware.ResilienceContext] = providedContext
            });
            var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

            AmanhecerContext? seenByNext = null;
            next.Invoke(Arg.Any<AmanhecerContext>())
                .Returns(callInfo =>
                {
                    seenByNext = callInfo.Arg<AmanhecerContext>();
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
    public async Task When_ExecuteAsync_WhenNotInitialized_Should_ThrowInvalidOperation()
    {
        var middleware = new PollyResiliencePipelineMiddleware(CreateProvider());
        var context = CreateContext();
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        await Assert.That(async () => await middleware.ExecuteAsync(context, next))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task When_ExecuteAsync_WhenPipelineNameIsUnknown_Should_ThrowKeyNotFound()
    {
        var middleware = CreateMiddleware(CreateProvider(), "unknown");
        var context = CreateContext();
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        await Assert.That(async () => await middleware.ExecuteAsync(context, next))
            .ThrowsExactly<KeyNotFoundException>();
    }

    [Test]
    public async Task When_Initialize_WhenMetadataIsAttribute_Should_ExecuteNamedPipeline()
    {
        var middleware = new PollyResiliencePipelineMiddleware(CreateProvider());
        middleware.Initialize(new PollyResiliencePipelineAttribute(RetryPipeline, 1));
        var context = CreateContext();
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        var calls = 0;
        next.Invoke(Arg.Any<AmanhecerContext>())
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

        await next.Received(2).Invoke(Arg.Any<AmanhecerContext>());
    }

    [Test]
    public async Task When_SendAsync_WhenHandlerHasAttribute_Should_RetryViaNamedPipelineFromDI()
    {
        var state = new FlakyRequestState();
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(state);
        services.AddSingleton<ResiliencePipelineProvider<string>>(CreateProvider());
        services.AddAmanhecer(configurator => configurator.AddRequestHandler<FlakyRequestHandler>());
        var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IDispatcher>().SendAsync(new TestRequest("request"));

        await Assert.That(state.Calls).IsEqualTo(2);
    }

    [Test]
    public async Task When_Initialize_WhenMetadataIsInvalid_Should_ThrowArgumentException()
    {
        var middleware = new PollyResiliencePipelineMiddleware(CreateProvider());

        await Assert.That(() => middleware.Initialize(null)).ThrowsExactly<ArgumentException>();
        await Assert.That(() => middleware.Initialize("")).ThrowsExactly<ArgumentException>();
        await Assert.That(() => middleware.Initialize(42)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task When_AttributeIsCreated_Should_ReturnMiddlewareTypeAndKeepOrderAndName()
    {
        var attribute = new PollyResiliencePipelineAttribute(RetryPipeline, 5);

        await Assert.That(attribute.GetMiddlewareType()).IsEqualTo(typeof(PollyResiliencePipelineMiddleware));
        await Assert.That(attribute.Order).IsEqualTo(5);
        await Assert.That(attribute.PipelineName).IsEqualTo(RetryPipeline);
    }

    public sealed record TestRequest(string Value);

    public sealed class FlakyRequestState
    {
        public int Calls;
    }

    [PollyResiliencePipeline(RetryPipeline, 1)]
    public sealed class FlakyRequestHandler(FlakyRequestState state) : RequestHandler<TestRequest>
    {
        public override ValueTask HandleAsync(
            TestRequest request,
            AmanhecerContext context,
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
