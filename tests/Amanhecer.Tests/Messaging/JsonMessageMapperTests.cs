using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Exceptions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Amanhecer.Messaging;

namespace Amanhecer.Tests.Messaging;

public class JsonMessageMapperTests
{
    [Test]
    public async Task GenericMapper_ToMessageAsync_Should_SerialisePayloadAndCopyContextIds()
    {
        var context = new AmanhecerContext();
        var mapper = new JsonMessageMapper<SampleRequest>(new JsonSerializerOptions());

        var message = await mapper.ToMessageAsync(new SampleRequest("order", 42), context);

        await Assert.That(Encoding.UTF8.GetString(message.Payload.Span))
            .IsEqualTo("""{"Name":"order","Value":42}""");
        await Assert.That(message.Id).IsEqualTo(context.RequestId);
        await Assert.That(message.CorrelationId).IsEqualTo(context.CorrelationId);
    }

    [Test]
    public async Task GenericMapper_ToRequestAsync_Should_DeserialisePayload()
    {
        var mapper = new JsonMessageMapper<SampleRequest>(new JsonSerializerOptions());
        var message = new Message
        {
            Payload = Encoding.UTF8.GetBytes("""{"Name":"order","Value":42}""")
        };

        var request = await mapper.ToRequestAsync(message, new AmanhecerContext());

        await Assert.That(request).IsEqualTo(new SampleRequest("order", 42));
    }

    [Test]
    public async Task GenericMapper_ToRequestAsync_When_PayloadIsNotJson_Should_ThrowInvalidMessageException()
    {
        var mapper = new JsonMessageMapper<SampleRequest>(new JsonSerializerOptions());
        var message = new Message { Payload = Encoding.UTF8.GetBytes("not-json") };

        await Assert.That(async () => await mapper.ToRequestAsync(message, new AmanhecerContext()))
            .Throws<InvalidMessageException>();
    }

    [Test]
    public async Task ToMessageAsync_Should_SerialiseUsingTheRuntimeType()
    {
        var context = new AmanhecerContext();
        var mapper = new JsonMessageMapper();
        object request = new DerivedRequest("order", 42, "extra");

        var message = await mapper.ToMessageAsync(request, context);

        await Assert.That(Encoding.UTF8.GetString(message.Payload.Span))
            .IsEqualTo("""{"Name":"order","Value":42,"Extra":"extra"}""");
        await Assert.That(message.Id).IsEqualTo(context.RequestId);
        await Assert.That(message.CorrelationId).IsEqualTo(context.CorrelationId);
    }

    [Test]
    public async Task ToRequestAsync_Should_DeserialiseToTheRequestTypeMetadata()
    {
        var context = new AmanhecerContext();
        context.SetMetadata(typeof(SampleRequest), MetadataName.RequestType);
        var mapper = new JsonMessageMapper();
        var message = new Message
        {
            Payload = Encoding.UTF8.GetBytes("""{"Name":"order","Value":42}""")
        };

        var request = await mapper.ToRequestAsync(message, context);

        await Assert.That(request).IsEqualTo(new SampleRequest("order", 42));
    }

    [Test]
    public async Task ToRequestAsync_When_RequestTypeMetadataMissing_Should_ThrowKeyNotFoundException()
    {
        var mapper = new JsonMessageMapper();
        var message = new Message
        {
            Payload = Encoding.UTF8.GetBytes("""{"Name":"order","Value":42}""")
        };

        await Assert.That(async () => await mapper.ToRequestAsync(message, new AmanhecerContext()))
            .Throws<KeyNotFoundException>();
    }

    [Test]
    public async Task ToRequestAsync_When_PayloadIsNotJson_Should_ThrowInvalidMessageException()
    {
        var context = new AmanhecerContext();
        context.SetMetadata(typeof(SampleRequest), MetadataName.RequestType);
        var mapper = new JsonMessageMapper();
        var message = new Message { Payload = Encoding.UTF8.GetBytes("not-json") };

        await Assert.That(async () => await mapper.ToRequestAsync(message, context))
            .Throws<InvalidMessageException>();
    }

    private sealed record SampleRequest(string Name, int Value);

    private sealed record DerivedRequest(string Name, int Value, string Extra);
}
