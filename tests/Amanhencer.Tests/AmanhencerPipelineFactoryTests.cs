using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Amanhencer.Abstractions;
using Amanhencer.Configurator;
using NSubstitute;

namespace Amanhencer.Tests;

public class AmanhencerPipelineFactoryTests
{
    private readonly IMiddlewareFactory  _middlewareFactory = Substitute.For<IMiddlewareFactory>();

    [Test]
    [RequiresUnreferencedCode("")]
    public async Task When_Create_Should_ReturnEmpty()
    {
        var options = new Dictionary<string, ImmutableList<ImmutableList<AmanhencerMiddlewareOptions>>>()
            .ToFrozenDictionary();
        
        var pipelineFactory = new AmanhencerPipelineFactory(
            new AmanhencerPipelineOptions(options), 
            _middlewareFactory);
        
        var context = Substitute.For<IPipelineContext >();
        await Assert.That(() => pipelineFactory.Create(context))
            .ThrowsNothing()
            .And.IsEquivalentTo(ImmutableList<IPipeline>.Empty);
        
        _middlewareFactory
            .DidNotReceive()
            .Create(Arg.Any<Type>(), Arg.Any<object?>());
    }
}