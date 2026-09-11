using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Extensions;
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

namespace Amanhecer.Extensions.Resilience.Tests;

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

    private static ResiliencePipelineMiddleware CreateMiddleware(ResiliencePipelineProvider<string> provider)
    {
        return new ResiliencePipelineMiddleware(provider);
    }

    private static AmanhecerContext CreateContext(
        string? pipelineName = null,
        CancellationToken cancellationToken = default)
    {
        var context = new AmanhecerContext
        {
            CancellationToken = cancellationToken,
            Request = new TestRequest("request")
        };

        if (pipelineName != null)
        {
            context.SetMetadata(new ResiliencePipelineMetadata(pipelineName));
        }

        return context;
    }

    [Test]
    public async Task When_ExecuteAsync_WhenNextFails_Should_RetryUntilItSucceeds()
    {
        var middleware = CreateMiddleware(CreateProvider());
        var context = CreateContext(RetryPipeline);
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
        var middleware = CreateMiddleware(CreateProvider());
        var context = CreateContext(RetryPipeline);
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();
        next.Invoke(Arg.Any<AmanhecerContext>()).Throws(new InvalidOperationException("Boom."));

        await Assert.That(async () => await middleware.ExecuteAsync(context, next))
            .ThrowsExactly<InvalidOperationException>();

        await next.Received(3).Invoke(Arg.Any<AmanhecerContext>());
    }

    [Test]
    public async Task When_ExecuteAsync_WhenExceptionIsNotRetryable_Should_NotRetry()
    {
        var middleware = CreateMiddleware(CreateProvider());
        var context = CreateContext(RetryPipeline);
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();
        next.Invoke(Arg.Any<AmanhecerContext>()).Throws(new ArgumentException("Not handled."));

        await Assert.That(async () => await middleware.ExecuteAsync(context, next))
            .ThrowsExactly<ArgumentException>();

        await next.Received(1).Invoke(Arg.Any<AmanhecerContext>());
    }

    [Test]
    public async Task When_ExecuteAsync_WhenExecutionExceedsTimeout_Should_ThrowTimeoutRejected()
    {
        var middleware = CreateMiddleware(CreateProvider());
        var context = CreateContext(TimeoutPipeline);
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
        var middleware = CreateMiddleware(CreateProvider());
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        var context = CreateContext(RetryPipeline, cancellationTokenSource.Token);
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        await Assert.That(async () => await middleware.ExecuteAsync(context, next))
            .ThrowsExactly<OperationCanceledException>();

        await next.DidNotReceive().Invoke(Arg.Any<AmanhecerContext>());
    }

    [Test]
    public async Task When_ExecuteAsync_Should_PassCallerCancellationTokenToNext()
    {
        var middleware = CreateMiddleware(CreateProvider());
        using var cancellationTokenSource = new CancellationTokenSource();
        var context = CreateContext(PassThroughPipeline, cancellationTokenSource.Token);
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        CancellationToken? seenByNext = null;
        next.Invoke(Arg.Any<AmanhecerContext>())
            .Returns(callInfo =>
            {
                seenByNext = callInfo.Arg<AmanhecerContext>().CancellationToken;
                return ValueTask.CompletedTask;
            });

        await middleware.ExecuteAsync(context, next);

        await Assert.That(seenByNext).IsNotNull();
        await Assert.That(seenByNext!.Value).IsEqualTo(cancellationTokenSource.Token);
    }

    [Test]
    public async Task When_ExecuteAsync_WhenResilienceContextIsProvidedInMetadata_Should_UseIt()
    {
        var middleware = CreateMiddleware(CreateProvider());
        using var cancellationTokenSource = new CancellationTokenSource();
        var providedContext = ResilienceContextPool.Shared.Get(cancellationTokenSource.Token);
        try
        {
            var context = CreateContext(PassThroughPipeline);
            context.SetMetadata(providedContext, ResiliencePipelineMiddleware.ResilienceContext);
            var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

            CancellationToken? seenByNext = null;
            next.Invoke(Arg.Any<AmanhecerContext>())
                .Returns(callInfo =>
                {
                    seenByNext = callInfo.Arg<AmanhecerContext>().CancellationToken;
                    return ValueTask.CompletedTask;
                });

            await middleware.ExecuteAsync(context, next);

            await Assert.That(seenByNext).IsNotNull();
            await Assert.That(seenByNext!.Value).IsEqualTo(cancellationTokenSource.Token);
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
        var middleware = CreateMiddleware(provider);
        var context = CreateContext("capturing");
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
        var middleware = CreateMiddleware(provider);
        var providedContext = ResilienceContextPool.Shared.Get();
        providedContext.SetRequestMetadata(new RequestMetadata { RequestName = "caller.name" });
        try
        {
            var context = CreateContext("capturing");
            context.SetMetadata(providedContext, ResiliencePipelineMiddleware.ResilienceContext);
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

            await Assert.That(capturedContext?.GetRequestMetadata()?.RequestName).IsEqualTo("caller.name");
        }
        finally
        {
            ResilienceContextPool.Shared.Return(providedContext);
        }
    }

    [Test]
    public async Task When_ExecuteAsync_WhenNoPipelineNameMetadata_Should_ThrowInvalidOperation()
    {
        var middleware = CreateMiddleware(CreateProvider());
        var context = CreateContext();
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        await Assert.That(async () => await middleware.ExecuteAsync(context, next))
            .ThrowsExactly<InvalidOperationException>();

        await next.DidNotReceive().Invoke(Arg.Any<AmanhecerContext>());
    }

    [Test]
    public async Task When_ExecuteAsync_WhenPipelineNameIsUnknown_Should_ThrowKeyNotFound()
    {
        var middleware = CreateMiddleware(CreateProvider());
        var context = CreateContext("unknown");
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        await Assert.That(async () => await middleware.ExecuteAsync(context, next))
            .ThrowsExactly<KeyNotFoundException>();
    }

    [Test]
    public async Task When_ExecuteAsync_WhenMetadataIsAttribute_Should_ExecuteNamedPipeline()
    {
        var middleware = CreateMiddleware(CreateProvider());
        var context = CreateContext();
        context.SetMetadata(new ResiliencePipelineAttribute(RetryPipeline, 1));
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
        services.AddResiliencePipeline(RetryPipeline, builder => builder.AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>(),
            MaxRetryAttempts = 2,
            Delay = TimeSpan.Zero
        }));
        services.AddAmanhecer(configurator => configurator.AddRequestHandler<FlakyRequestHandler>());
        var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IDispatcher>().SendAsync(new TestRequest("request"));

        await Assert.That(state.Calls).IsEqualTo(2);
    }

    [Test]
    public async Task When_AttributeIsCreated_Should_ReturnMiddlewareTypeAndKeepOrderAndName()
    {
        var attribute = new ResiliencePipelineAttribute(RetryPipeline, 5);

        await Assert.That(attribute.GetMiddlewareType()).IsEqualTo(typeof(ResiliencePipelineMiddleware));
        await Assert.That(attribute.Order).IsEqualTo(5);
        await Assert.That(attribute.PipelineName).IsEqualTo(RetryPipeline);
    }

    [Test]
    public async Task When_ExecuteAsync_WhenAttemptReplacesRequest_Should_RetryWithOriginalRequest()
    {
        var middleware = CreateMiddleware(CreateProvider());
        var context = CreateContext(RetryPipeline);
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        var encoded = 0;
        var shortCircuited = 0;
        next.Invoke(Arg.Any<AmanhecerContext>())
            .Returns(callInfo =>
            {
                var attempt = callInfo.Arg<AmanhecerContext>();
                if (attempt.Request is Message)
                {
                    shortCircuited++;
                    return ValueTask.CompletedTask;
                }

                encoded++;
                attempt.Request = new Message();
                if (encoded == 1)
                {
                    throw new InvalidOperationException("Boom.");
                }

                return ValueTask.CompletedTask;
            });

        await middleware.ExecuteAsync(context, next);

        await Assert.That(encoded).IsEqualTo(2);
        await Assert.That(shortCircuited).IsEqualTo(0);
        await Assert.That(context.Request).IsTypeOf<TestRequest>();
    }

    [Test]
    public async Task When_ExecuteAsync_Should_IsolateAttemptMutationsAndPropagateResponse()
    {
        var middleware = CreateMiddleware(CreateProvider());
        var context = CreateContext(RetryPipeline);
        var next = Substitute.For<Func<AmanhecerContext, ValueTask>>();

        var attemptContexts = new List<AmanhecerContext>();
        var markersSeen = new List<bool>();
        next.Invoke(Arg.Any<AmanhecerContext>())
            .Returns(callInfo =>
            {
                var attempt = callInfo.Arg<AmanhecerContext>();
                attemptContexts.Add(attempt);
                markersSeen.Add(attempt.Metadata.ContainsKey("attempt.marker"));
                attempt.Metadata["attempt.marker"] = true;
                attempt.Response = $"response-{attemptContexts.Count}";
                if (attemptContexts.Count == 1)
                {
                    throw new InvalidOperationException("Boom.");
                }

                return ValueTask.CompletedTask;
            });

        await middleware.ExecuteAsync(context, next);

        await Assert.That(markersSeen[0]).IsFalse();
        await Assert.That(markersSeen[1]).IsFalse();
        await Assert.That(attemptContexts[0]).IsNotSameReferenceAs(attemptContexts[1]);
        await Assert.That(attemptContexts[1]).IsNotSameReferenceAs(context);
        await Assert.That(context.Metadata.ContainsKey("attempt.marker")).IsFalse();
        await Assert.That(context.Response).IsEqualTo("response-2");
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
