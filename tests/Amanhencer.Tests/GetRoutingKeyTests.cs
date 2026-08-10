using System;
using System.Threading.Tasks;
using Amanhencer.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Amanhencer.Tests;

public class GetRoutingKeyTests
{
    [Test]
    public async Task AddRequestHandler_RequestAndQueryHandler_ThrowsNotSupportedException()
    {
        var services = new ServiceCollection();

        await Assert.That(() => services.AddAmanhencer(cfg => cfg.AddRequestHandler<MultiInterfaceHandler>()))
            .ThrowsExactly<NotSupportedException>();
    }

    [Test]
    public async Task AddRequestHandler_TwoRequestHandlerInterfaces_ThrowsNotSupportedException()
    {
        var services = new ServiceCollection();

        await Assert.That(() => services.AddAmanhencer(cfg => cfg.AddRequestHandler<TwoRequestTypesHandler>()))
            .ThrowsExactly<NotSupportedException>();
    }

    [Test]
    public async Task AddRequestHandler_NoGenericHandlerInterface_ThrowsArgumentException()
    {
        var services = new ServiceCollection();

        await Assert.That(() => services.AddAmanhencer(cfg => cfg.AddRequestHandler<MarkerOnlyRequestHandler>()))
            .ThrowsExactly<ArgumentException>();
    }
}
