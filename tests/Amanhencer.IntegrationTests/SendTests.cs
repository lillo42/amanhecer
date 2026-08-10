using System;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Configurator;

namespace Amanhencer.IntegrationTests;

public class SendTests : BaseTests
{
    protected override void ConfigureAmanhencer(AmanhencerConfigurator configurator)
    {
        configurator
            .AddRequestHandler<OneSyncRequestHandler>()
            .AddRequestHandler<OneAsyncRequestHandler>()
            .AddRequestHandler<FirstMultiRequestHandler>()
            .AddRequestHandler<SecondMultiRequestHandler>();
    }

    private record NoRequestHandler;

    private record OneSyncRequest;
    
    private class OneSyncRequestHandler : RequestHandler<OneSyncRequest>
    {
        public override ValueTask HandleAsync(OneSyncRequest request, IPipelineContext context, CancellationToken cancellationToken = default)
        {
            return new ValueTask();
        }
    }
    
    private record OneAsyncRequest;
    
    private class OneAsyncRequestHandler : RequestHandler<OneAsyncRequest>
    {
        public override async ValueTask HandleAsync(OneAsyncRequest request, IPipelineContext context, CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }


    private record MultiRequest;
    
    private class FirstMultiRequestHandler : RequestHandler<MultiRequest>
    {
        public override ValueTask HandleAsync(MultiRequest request, IPipelineContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
    
    private class SecondMultiRequestHandler : RequestHandler<MultiRequest>
    {
        public override ValueTask HandleAsync(MultiRequest request, IPipelineContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}