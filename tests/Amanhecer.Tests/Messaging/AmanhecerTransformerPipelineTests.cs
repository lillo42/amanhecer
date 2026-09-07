using System.Collections.Generic;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging;

namespace Amanhecer.Tests.Messaging;

public class AmanhecerTransformerPipelineTests
{
    [Test]
    public async Task EncodeTransformerPipeline_Should_RunTransformersInOrder()
    {
        var recorder = new List<string>();
        var context = new AmanhecerContext();
        context.SetMetadata(recorder);

        var pipeline = new AmanhecerEncodeTransformerPipeline([new EncodeOnlyTransformer(), new BothWaysTransformer()]);

        await pipeline.EncodeAsync(new Message(), context);

        await Assert.That(recorder.Count).IsEqualTo(2);
        await Assert.That(recorder[0]).IsEqualTo(EncodeOnlyTransformer.Name);
        await Assert.That(recorder[1]).IsEqualTo(BothWaysTransformer.EncodeName);
    }

    [Test]
    public async Task DecodeTransformerPipeline_Should_RunTransformersInOrder()
    {
        var recorder = new List<string>();
        var context = new AmanhecerContext();
        context.SetMetadata(recorder);

        var pipeline = new AmanhecerDecodeTransformerPipeline([new DecodeOnlyTransformer(), new BothWaysTransformer()]);

        await pipeline.DecodeAsync(new Message(), context);

        await Assert.That(recorder.Count).IsEqualTo(2);
        await Assert.That(recorder[0]).IsEqualTo(DecodeOnlyTransformer.Name);
        await Assert.That(recorder[1]).IsEqualTo(BothWaysTransformer.DecodeName);
    }
}
