using System;
using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Tests;

public class QueryHandlerTests
{
    [Test]
    public async Task HandleAsync_ObjectOverload_Should_CastAndDelegateToTypedOverload()
    {
        var handler = new LengthQueryHandler();
        var context = new AmanhecerContext();

        var response = await handler.HandleAsync((object)"hello", context, CancellationToken.None);

        await Assert.That(response).IsEqualTo(5);
        await Assert.That(ReferenceEquals(handler.LastContext, context)).IsTrue();
    }

    [Test]
    public async Task HandleAsync_ObjectOverloadWithWrongType_Should_ThrowInvalidCastException()
    {
        var handler = new LengthQueryHandler();
        var context = new AmanhecerContext();

        await Assert.That(async () => await handler.HandleAsync(42, context, CancellationToken.None))
            .Throws<InvalidCastException>();
    }

    private class LengthQueryHandler : QueryHandler<string, int>
    {
        public AmanhecerContext? LastContext { get; private set; }

        public override ValueTask<int> HandleAsync(string query, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            LastContext = context;
            return new ValueTask<int>(query.Length);
        }
    }
}
