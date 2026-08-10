using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.ExecutingStrategies;
using Microsoft.Extensions.Logging.Abstractions;

namespace Amanhencer.Polly.Tests;

public record TestRequest(string Value);

public sealed class FlakyRequestState
{
    public int Calls;
}

[PollyResiliencePipeline("retry", 1)]
public class FlakyRequestHandler(FlakyRequestState state) : RequestHandler<TestRequest>
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

public static class TestPipelineContext
{
    public static AmanhencerPipelineContext Create(
        object? request = null,
        string routingKey = "test.key",
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        return new AmanhencerPipelineContext(
            null,
            [],
            metadata ?? new Dictionary<string, object>(),
            routingKey,
            request ?? new TestRequest("request"),
            new SequenceExecutingStrategy(new NullLogger<SequenceExecutingStrategy>()),
            cancellationToken);
    }
}
