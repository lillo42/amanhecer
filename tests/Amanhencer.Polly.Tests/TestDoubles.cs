using System.Collections.Generic;
using System.Threading;
using Amanhencer.Abstractions;
using Amanhencer.ExecutingStrategies;

namespace Amanhencer.Polly.Tests;

public record TestRequest(string Value);

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
            new SequenceExecutingStrategy(),
            cancellationToken);
    }
}
