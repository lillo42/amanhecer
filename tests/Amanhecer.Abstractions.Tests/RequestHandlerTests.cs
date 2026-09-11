using System;
using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Tests;

public class RequestHandlerTests
{
    [Test]
    public async Task HandleAsync_ObjectOverload_Should_CastAndDelegateToTypedOverload()
    {
        var handler = new RecordingRequestHandler();
        var context = new AmanhecerContext();

        await handler.HandleAsync((object)"the-request", context, CancellationToken.None);

        await Assert.That(handler.Handled).IsEqualTo("the-request");
        await Assert.That(ReferenceEquals(handler.LastContext, context)).IsTrue();
    }

    [Test]
    public async Task HandleAsync_ObjectOverloadWithWrongType_Should_ThrowInvalidCastException()
    {
        var handler = new RecordingRequestHandler();
        var context = new AmanhecerContext();

        await Assert.That(async () => await handler.HandleAsync(42, context, CancellationToken.None))
            .Throws<InvalidCastException>();
    }

    private class RecordingRequestHandler : RequestHandler<string>
    {
        public string? Handled { get; private set; }

        public AmanhecerContext? LastContext { get; private set; }

        public override ValueTask HandleAsync(string request, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            Handled = request;
            LastContext = context;
            return default;
        }
    }
}
