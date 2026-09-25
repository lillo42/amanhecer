using System;
using System.Text;
using System.Threading.Tasks;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Abstractions.Tests.Messaging;

public class MessageMapperTests
{
    [Test]
    public async Task ToMessageAsync_ObjectOverload_Should_CastAndDelegateToTypedOverload()
    {
        IMessageMapper mapper = new StringMessageMapper();
        var context = new AmanhecerContext();

        var message = await mapper.ToMessageAsync("hello", context);

        await Assert.That(Encoding.UTF8.GetString(message.Payload.Span)).IsEqualTo("hello");
        await Assert.That(message.Subject).IsEqualTo("string-message");
    }

    [Test]
    public async Task ToMessageAsync_ObjectOverloadWithWrongType_Should_ThrowInvalidCastException()
    {
        IMessageMapper mapper = new StringMessageMapper();
        var context = new AmanhecerContext();

        await Assert.That(async () => await mapper.ToMessageAsync(42, context))
            .Throws<InvalidCastException>();
    }

    [Test]
    public async Task ToRequestAsync_ObjectOverload_Should_ReturnMappedRequest()
    {
        IMessageMapper mapper = new StringMessageMapper();
        var context = new AmanhecerContext();
        var message = new Message { Payload = "world"u8.ToArray() };

        var request = await mapper.ToRequestAsync(message, context);

        await Assert.That(request).IsEqualTo("world");
    }

    [Test]
    public async Task ToRequestAsync_TypedOverload_Should_ReturnMappedRequest()
    {
        var mapper = new StringMessageMapper();
        var context = new AmanhecerContext();
        var message = new Message { Payload = "typed"u8.ToArray() };

        var request = await mapper.ToRequestAsync(message, context);

        await Assert.That(request).IsEqualTo("typed");
    }

    private class StringMessageMapper : MessageMapper<string>
    {
        public override ValueTask<Message> ToMessageAsync(string request, AmanhecerContext context)
        {
            return new ValueTask<Message>(new Message
            {
                Payload = Encoding.UTF8.GetBytes(request),
                Subject = "string-message"
            });
        }

        public override ValueTask<string> ToRequestAsync(Message message, AmanhecerContext context)
        {
            return new ValueTask<string>(Encoding.UTF8.GetString(message.Payload.Span));
        }
    }
}
