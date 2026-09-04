using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Exceptions;
using Amanhecer.Configurator;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhecer.IntegrationTests;

public class QueryTests : BaseTests
{
    protected override void ConfigureAmanhecer(AmanhecerConfigurator configurator)
    {
        configurator
            .AddQueryHandler<OneSyncQueryHandler>()
            .AddQueryHandler<OneAsyncQueryHandler>()
            .AddQueryHandler<FirstMultiQueryHandler>()
            .AddQueryHandler<SecondMultiQueryHandler>();
    }

    [Test]
    public async Task When_Query_AndNoHandlerRegistered_Should_ThrowPipelineNotFoundException()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await Assert.That(() => dispatcher.Query<NoQueryHandler, string>(new NoQueryHandler()))
            .Throws<PipelineNotFoundException>()
            .WithMessageContaining(typeof(NoQueryHandler).FullName!);
    }

    [Test]
    public async Task When_QueryAsync_AndNoHandlerRegistered_Should_ThrowPipelineNotFoundException()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await Assert.That(async () => await dispatcher.QueryAsync<NoQueryHandler, string>(new NoQueryHandler()))
            .Throws<PipelineNotFoundException>()
            .WithMessageContaining(typeof(NoQueryHandler).FullName!);
    }

    [Test]
    public async Task When_Query_Should_ReturnResponse()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        var response = dispatcher.Query<OneSyncQuery, OneSyncResponse>(new OneSyncQuery("ping"));

        await Assert.That(response).IsEqualTo(new OneSyncResponse("ping"));
    }

    [Test]
    public async Task When_QueryAsync_Should_ReturnResponse()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        var response = await dispatcher.QueryAsync<OneAsyncQuery, OneAsyncResponse>(new OneAsyncQuery("ping"));

        await Assert.That(response).IsEqualTo(new OneAsyncResponse("ping"));
    }

    [Test]
    public async Task When_Query_Should_ReturnResponseFromAsyncHandler()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        var response = dispatcher.Query<OneAsyncQuery, OneAsyncResponse>(new OneAsyncQuery("ping"));

        await Assert.That(response).IsEqualTo(new OneAsyncResponse("ping"));
    }

    [Test]
    public async Task When_Query_AndMultipleHandlersRegistered_Should_ThrowMultiPipelineFoundException()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await Assert.That(() => dispatcher.Query<MultiQuery, string>(new MultiQuery()))
            .Throws<MultiPipelineFoundException>()
            .WithMessageContaining(typeof(MultiQuery).FullName!);
    }

    [Test]
    public async Task When_QueryAsync_AndMultipleHandlersRegistered_Should_ThrowMultiPipelineFoundException()
    {
        var dispatcher = ServiceProvider.GetRequiredService<IDispatcher>();

        await Assert.That(async () => await dispatcher.QueryAsync<MultiQuery, string>(new MultiQuery()))
            .Throws<MultiPipelineFoundException>()
            .WithMessageContaining(typeof(MultiQuery).FullName!);
    }

    private record NoQueryHandler;

    private record OneSyncQuery(string Value);

    private record OneSyncResponse(string Value);

    private class OneSyncQueryHandler : QueryHandler<OneSyncQuery, OneSyncResponse>
    {
        public override ValueTask<OneSyncResponse> HandleAsync(OneSyncQuery query, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            return new ValueTask<OneSyncResponse>(new OneSyncResponse(query.Value));
        }
    }

    private record OneAsyncQuery(string Value);

    private record OneAsyncResponse(string Value);

    private class OneAsyncQueryHandler : QueryHandler<OneAsyncQuery, OneAsyncResponse>
    {
        public override async ValueTask<OneAsyncResponse> HandleAsync(OneAsyncQuery query, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            return new OneAsyncResponse(query.Value);
        }
    }

    private record MultiQuery;

    private class FirstMultiQueryHandler : QueryHandler<MultiQuery, string>
    {
        public override ValueTask<string> HandleAsync(MultiQuery query, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    private class SecondMultiQueryHandler : QueryHandler<MultiQuery, string>
    {
        public override ValueTask<string> HandleAsync(MultiQuery query, AmanhecerContext context,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
