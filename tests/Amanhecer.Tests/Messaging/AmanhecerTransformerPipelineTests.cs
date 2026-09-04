using System.Collections.Generic;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging;
using NSubstitute;

namespace Amanhecer.Tests.Messaging;

public class AmanhecerTransformerPipelineTests
{
    [Test]
    public async Task EncodeTransformerPipeline_Should_RunTransformersInOrder()
    {
        var recorder = new List<string>();
        var encode = new EncodeOnlyTransformer();
        encode.Initialize(recorder);
        var both = new BothWaysTransformer();
        both.Initialize(recorder);

        var pipeline = new AmanhecerEncodeTransformerPipeline([encode, both]);

        await pipeline.EncodeAsync(new Message(), Substitute.For<AmanhecerContext>());

        await Assert.That(recorder.Count).IsEqualTo(2);
        await Assert.That(recorder[0]).IsEqualTo(EncodeOnlyTransformer.Name);
        await Assert.That(recorder[1]).IsEqualTo(BothWaysTransformer.EncodeName);
    }

    [Test]
    public async Task DecodeTransformerPipeline_Should_RunTransformersInOrder()
    {
        var recorder = new List<string>();
        var decode = new DecodeOnlyTransformer();
        decode.Initialize(recorder);
        var both = new BothWaysTransformer();
        both.Initialize(recorder);

        var pipeline = new AmanhecerDecodeTransformerPipeline([decode, both]);

        await pipeline.DecodeAsync(new Message(), Substitute.For<AmanhecerContext>());

        await Assert.That(recorder.Count).IsEqualTo(2);
        await Assert.That(recorder[0]).IsEqualTo(DecodeOnlyTransformer.Name);
        await Assert.That(recorder[1]).IsEqualTo(BothWaysTransformer.DecodeName);
    }
}
