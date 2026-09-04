using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Tests.Messaging;

internal sealed class EncodeOnlyTransformer : IEncodeTransformer
{
    public const string Name = "encode-only";

    private List<string>? _recorder;

    public void Initialize(object? metadata)
    {
        _recorder = metadata as List<string>;
    }

    public ValueTask EncodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        _recorder?.Add(Name);
        return next(message, context);
    }
}

internal sealed class AnotherEncodeTransformer : IEncodeTransformer
{
    public const string Name = "another-encode";

    private List<string>? _recorder;

    public void Initialize(object? metadata)
    {
        _recorder = metadata as List<string>;
    }

    public ValueTask EncodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        _recorder?.Add(Name);
        return next(message, context);
    }
}

internal sealed class DecodeOnlyTransformer : IDecodeTransformer
{
    public const string Name = "decode-only";

    private List<string>? _recorder;

    public void Initialize(object? metadata)
    {
        _recorder = metadata as List<string>;
    }

    public ValueTask DecodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        _recorder?.Add(Name);
        return next(message, context);
    }
}

internal sealed class BothWaysTransformer : ITransformer
{
    public const string EncodeName = "both-encode";
    public const string DecodeName = "both-decode";

    private List<string>? _recorder;

    public void Initialize(object? metadata)
    {
        _recorder = metadata as List<string>;
    }

    public ValueTask EncodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        _recorder?.Add(EncodeName);
        return next(message, context);
    }

    public ValueTask DecodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        _recorder?.Add(DecodeName);
        return next(message, context);
    }
}

internal sealed class TestTransformerAttribute(int order) : TransformerAttribute(order)
{
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public override Type GetTransformerType()
    {
        return typeof(BothWaysTransformer);
    }
}

internal sealed class InvalidTransformerAttribute(int order) : TransformerAttribute(order)
{
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public override Type GetTransformerType()
    {
        return typeof(string);
    }
}

[TestTransformer(5)]
internal sealed class AttributedMapper : IMessageMapper
{
    public ValueTask<Message> ToMessageAsync(object request, AmanhecerContext context)
    {
        return new ValueTask<Message>(new Message());
    }

    public ValueTask<object> ToRequestAsync(Message message, AmanhecerContext context)
    {
        return new ValueTask<object>(new object());
    }
}

[InvalidTransformer(5)]
internal sealed class InvalidAttributedMapper : IMessageMapper
{
    public ValueTask<Message> ToMessageAsync(object request, AmanhecerContext context)
    {
        return new ValueTask<Message>(new Message());
    }

    public ValueTask<object> ToRequestAsync(Message message, AmanhecerContext context)
    {
        return new ValueTask<object>(new object());
    }
}

internal sealed class TestPublication : Publication;
