using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using NSubstitute;

namespace Amanhencer.Tests;

public class AmanhencerPipelineContextFactoryTests
{
    private readonly IExecutingStrategy _executingStrategy;
    private readonly AmanhencerPipelineContextFactory _factory;

    public AmanhencerPipelineContextFactoryTests()
    {
        _executingStrategy = Substitute.For<IExecutingStrategy>();
        _factory = new AmanhencerPipelineContextFactory(_executingStrategy);
    }

    [Test]
    [Arguments(typeof(SomeRequest), null, "Amanhencer.Tests.AmanhencerPipelineContextFactoryTests+SomeRequest")]
    [Arguments(typeof(SomeRequest), "random-name", "random-name")]
    [Arguments(typeof(SomeRequestWithAttribute), "random-name", "random-name")]
    [Arguments(typeof(SomeRequestWithAttribute), null, "some-request")]
    public async Task When_Create_Should_ResolveTheRoutingKeyCorrectly(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        Type type, string? routingKey, string expectedName)
    {
        var context = Substitute.For<IContext>();
        context.RoutingKey.Returns(routingKey);
        context.ExecutingStrategy.Returns((IExecutingStrategy?)null);
        var request = Activator.CreateInstance(type)!;

        await Assert.That(() => _factory.Create(request, context, CancellationToken.None))
            .ThrowsNothing()
            .And.Member(x => x.RoutingKey, y => y.IsEqualTo(expectedName))
            .And.Member(x => x.ExecutingStrategy, y => y.IsEqualTo(_executingStrategy));
    }
    
    [Test]
    public async Task When_Create_Should_ResolveTheExecutingStrategyCorrectly()
    {
        var context = Substitute.For<IContext>();
        var executingStrategy = Substitute.For<IExecutingStrategy>();
        
        context.RoutingKey.Returns((string?)null);
        context.ExecutingStrategy.Returns(executingStrategy);
        
        var request = new SomeRequestWithAttribute();

        await Assert.That(() => _factory.Create(request, context, CancellationToken.None))
            .ThrowsNothing()
            .And.Member(x => x.ExecutingStrategy, y => y.IsEqualTo(executingStrategy));
    }

    public record SomeRequest;

    [RoutingKey("some-request")]
    public record SomeRequestWithAttribute;
}