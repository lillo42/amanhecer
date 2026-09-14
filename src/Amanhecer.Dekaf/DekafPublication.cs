using System;
using System.Text;
using System.Text.Json;
using Amanhecer.Abstractions.Messaging;

namespace Amanhecer.Dekaf;

public class DekafPublication : Publication
{
    public string Topic { get; set; }

    public Encoding Encoding { get; } = Encoding.UTF8;

    public Func<object, byte[]> ConvertToByteArray { get; set; } = obj => JsonSerializer.SerializeToUtf8Bytes(obj);
}
