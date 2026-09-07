using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Options;
using Amanhecer.Configurator;
using NSubstitute;

namespace Amanhecer.Tests;

public class AmanhecerPipelineFactoryTests
{
    private readonly IMiddlewareFactory  _middlewareFactory = Substitute.For<IMiddlewareFactory>();

    [Test]
    [RequiresUnreferencedCode(
        "Collection equivalency uses structural comparison for complex objects, " +
        "which requires reflection and is not compatible with AOT.")]
    public async Task When_Create_Should_ReturnEmpty()
    {
        var options = new Dictionary<string, List<IEnumerable<AmanhecerMiddlewareOptions>>>()
            .ToFrozenDictionary();

        var pipelineFactory = new AmanhecerPipelineFactory(
            new AmanhecerPipelineOptions(options),
            _middlewareFactory);

        var context = new AmanhecerContext();
        await Assert.That(() => pipelineFactory.Create(context))
            .ThrowsNothing()
            .And.IsEquivalentTo(ImmutableList<IPipeline>.Empty);

        _middlewareFactory
            .DidNotReceive()
            .Create(Arg.Any<Type>(), Arg.Any<object?>(), Arg.Any<AmanhecerContext>());
    }

    [Test]
    public async Task When_Create_Should_ReturnOnePipeline()
    {
        var middlewareType = typeof(AmanhecerPipelineFactoryTests);
        object? metadata = new { Key = "value" };
        var options = new Dictionary<string, List<IEnumerable<AmanhecerMiddlewareOptions>>>
            {
                ["key"] =
                [
                    [new AmanhecerMiddlewareOptions(middlewareType, 0, metadata)]
                ]
            }
            .ToFrozenDictionary();

        var pipelineFactory = new AmanhecerPipelineFactory(
            new AmanhecerPipelineOptions(options),
            _middlewareFactory);

        var context = new AmanhecerContext { RoutingKey = "key" };

        var pipelines = pipelineFactory.Create(context);

        await Assert.That(pipelines.Count).IsEqualTo(1);

        _middlewareFactory
            .Received(1)
            .Create(middlewareType, metadata, Arg.Is<AmanhecerContext>(c => !ReferenceEquals(c, context)));
    }

    [Test]
    public async Task When_Create_Should_ReturnOnePipelinePerMiddlewareList()
    {
        var options = new Dictionary<string, List<IEnumerable<AmanhecerMiddlewareOptions>>>
            {
                ["key"] =
                [
                    [new AmanhecerMiddlewareOptions(typeof(string), 0, null)],
                    [new AmanhecerMiddlewareOptions(typeof(int), 1, "meta")]
                ]
            }
            .ToFrozenDictionary();

        var pipelineFactory = new AmanhecerPipelineFactory(
            new AmanhecerPipelineOptions(options),
            _middlewareFactory);

        var context = new AmanhecerContext { RoutingKey = "key" };

        var pipelines = pipelineFactory.Create(context);

        await Assert.That(pipelines.Count).IsEqualTo(2);

        _middlewareFactory
            .Received(1)
            .Create(
                Arg.Is<Type>(t => t == typeof(string)),
                Arg.Is<object?>(m => m == null),
                Arg.Is<AmanhecerContext>(c => !ReferenceEquals(c, context)));

        _middlewareFactory
            .Received(1)
            .Create(
                Arg.Is<Type>(t => t == typeof(int)),
                Arg.Is<object?>(m => Equals(m, "meta")),
                Arg.Is<AmanhecerContext>(c => !ReferenceEquals(c, context)));
    }

    [Test]
    [RequiresUnreferencedCode(
        "Collection equivalency uses structural comparison for complex objects, " +
        "which requires reflection and is not compatible with AOT.")]
    public async Task When_Create_Should_ReturnEmptyWhenRoutingKeyDoesNotMatch()
    {
        var options = new Dictionary<string, List<IEnumerable<AmanhecerMiddlewareOptions>>>
            {
                ["other"] =
                [
                    [new AmanhecerMiddlewareOptions(typeof(string), 0, null)]
                ]
            }
            .ToFrozenDictionary();

        var pipelineFactory = new AmanhecerPipelineFactory(
            new AmanhecerPipelineOptions(options),
            _middlewareFactory);

        var context = new AmanhecerContext { RoutingKey = "key" };

        await Assert.That(pipelineFactory.Create(context))
            .IsEquivalentTo(ImmutableList<IPipeline>.Empty);

        _middlewareFactory
            .DidNotReceive()
            .Create(Arg.Any<Type>(), Arg.Any<object?>(), Arg.Any<AmanhecerContext>());
    }

    [Test]
    public async Task When_Create_Should_ReturnPipelineWithNoMiddlewaresWhenListIsEmpty()
    {
        var options = new Dictionary<string, List<IEnumerable<AmanhecerMiddlewareOptions>>>
            {
                ["key"] =
                [
                    []
                ]
            }
            .ToFrozenDictionary();

        var pipelineFactory = new AmanhecerPipelineFactory(
            new AmanhecerPipelineOptions(options),
            _middlewareFactory);

        var context = new AmanhecerContext { RoutingKey = "key" };

        var pipelines = pipelineFactory.Create(context);

        await Assert.That(pipelines.Count).IsEqualTo(1);

        _middlewareFactory
            .DidNotReceive()
            .Create(Arg.Any<Type>(), Arg.Any<object?>(), Arg.Any<AmanhecerContext>());
    }
}
