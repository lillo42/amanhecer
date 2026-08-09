using Amanhencer.Abstractions;
using NSubstitute;

namespace Amanhencer.Tests;

public class AmanhencerDispatcherTests
{
    private readonly IPipelineContextFactory _pipelineContextFactory;
    private readonly IPipelineFactory _factoryFactory;

    public AmanhencerDispatcherTests()
    {
        _pipelineContextFactory = Substitute.For<IPipelineContextFactory>();
        _factoryFactory = Substitute.For<IPipelineFactory>();
    }
}