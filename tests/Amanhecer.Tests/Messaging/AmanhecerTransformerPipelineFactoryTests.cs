using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Configurator;
using Amanhecer.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Amanhecer.Tests.Messaging;

public class AmanhecerTransformerPipelineFactoryTests
{
    [Test]
    public async Task When_EncodePipelineNameUnknown_Should_RunEmptyPipeline()
    {
        var recorder = new List<string>();
        var factory = CreateEncodeFactory(new Dictionary<string, IReadOnlyList<AmanhecerTransformerOptions>>
        {
            ["known"] = [new AmanhecerTransformerOptions(typeof(EncodeOnlyTransformer), 0, recorder)]
        });

        var pipeline = factory.Create("does.not.exist", Substitute.For<IPipelineContext>());
        await pipeline.EncodeAsync(new Message(), Substitute.For<IPipelineContext>());

        await Assert.That(recorder).IsEmpty();
    }

    [Test]
    public async Task When_EncodePipelineExists_Should_RunOnlyEncodeTransformersInOrder()
    {
        var recorder = new List<string>();
        var factory = CreateEncodeFactory(new Dictionary<string, IReadOnlyList<AmanhecerTransformerOptions>>
        {
            ["known"] =
            [
                new AmanhecerTransformerOptions(typeof(EncodeOnlyTransformer), 0, recorder),
                new AmanhecerTransformerOptions(typeof(DecodeOnlyTransformer), 5, recorder),
                new AmanhecerTransformerOptions(typeof(AnotherEncodeTransformer), 10, recorder)
            ]
        });

        var pipeline = factory.Create("known", Substitute.For<IPipelineContext>());
        await pipeline.EncodeAsync(new Message(), Substitute.For<IPipelineContext>());

        await Assert.That(recorder.Count).IsEqualTo(2);
        await Assert.That(recorder[0]).IsEqualTo(EncodeOnlyTransformer.Name);
        await Assert.That(recorder[1]).IsEqualTo(AnotherEncodeTransformer.Name);
    }

    [Test]
    public async Task When_DecodePipelineExists_Should_RunOnlyDecodeTransformers()
    {
        var recorder = new List<string>();
        var factory = CreateDecodeFactory(new Dictionary<string, IReadOnlyList<AmanhecerTransformerOptions>>
        {
            ["known"] =
            [
                new AmanhecerTransformerOptions(typeof(EncodeOnlyTransformer), 0, recorder),
                new AmanhecerTransformerOptions(typeof(DecodeOnlyTransformer), 5, recorder)
            ]
        });

        var pipeline = factory.Create("known", Substitute.For<IPipelineContext>());
        await pipeline.DecodeAsync(new Message(), Substitute.For<IPipelineContext>());

        await Assert.That(recorder.Count).IsEqualTo(1);
        await Assert.That(recorder[0]).IsEqualTo(DecodeOnlyTransformer.Name);
    }

    private static AmanhecerEncodeTransformerPipelineFactory CreateEncodeFactory(
        Dictionary<string, IReadOnlyList<AmanhecerTransformerOptions>> named)
    {
        return new AmanhecerEncodeTransformerPipelineFactory(
            new AmanhecerTransformerPipelineOptions(named.ToFrozenDictionary()),
            new AmanhecerEncodeTransformerFactory(BuildProvider()));
    }

    private static AmanhecerDecodeTransformerPipelineFactory CreateDecodeFactory(
        Dictionary<string, IReadOnlyList<AmanhecerTransformerOptions>> named)
    {
        return new AmanhecerDecodeTransformerPipelineFactory(
            new AmanhecerTransformerPipelineOptions(named.ToFrozenDictionary()),
            new AmanhecerDecodeTransformerFactory(BuildProvider()));
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddTransient<EncodeOnlyTransformer>();
        services.AddTransient<AnotherEncodeTransformer>();
        services.AddTransient<DecodeOnlyTransformer>();
        services.AddTransient<BothWaysTransformer>();

        return services.BuildServiceProvider();
    }
}
