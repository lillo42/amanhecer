using System;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace Amanhecer.Messaging.Transformers;

public class CloudeventAttribute(int order) : TransformeAttribute<CloudeventTransformer>(order)
{
    public string? ContentType { get; set; }
    public string? DataSchema { get; set; }
    public string? ReplyTo { get; set; }
    public string? Source { get; set; }
    public string? SpecVersion { get; set; }
    public string? Subject { get; set; }
    public string? Type { get; set; }
}

public partial class CloudeventTransformer(ILogger<CloudeventTransformer> logger) : IEncodeTransformer
{
    private CloudeventAttribute? _attribute;

    public void Initialize(object? metadata)
    {
        if (metadata is CloudeventAttribute attribute)
        {
            _attribute = attribute;
        }
    }

    public async ValueTask EncodeAsync(Message message, IPipelineContext context,
        Func<Message, IPipelineContext, ValueTask> next)
    {
        if (_attribute != null)
        {
            Apply(message, _attribute);
        }

        var publication = context.Metadata.GetOrDefault<IPublication>(MetadataName.Publication);
        if (publication != null)
        {
            Apply(message, publication);
        }

        await next(message, context);
    }

    private void Apply(Message message, CloudeventAttribute attribute)
    {
        if (!string.IsNullOrEmpty(attribute.ContentType) && message.ContentType == null)
        {
            message.ContentType = new ContentType(attribute.ContentType);
        }

        if (!string.IsNullOrEmpty(attribute.DataSchema) && message.DataSchema == null)
        {
            if (Uri.TryCreate(attribute.DataSchema, UriKind.RelativeOrAbsolute, out var dataSchemaUri))
            {
                message.DataSchema = dataSchemaUri;
            }
            else
            {
                Logger.InvalidDataSchema(logger, attribute.DataSchema);
            }
        }

        if (!string.IsNullOrEmpty(attribute.ReplyTo) && message.ReplyTo == null)
        {
            message.ReplyTo = attribute.ReplyTo;
        }

        if (!string.IsNullOrEmpty(attribute.Source) && message.Source == null)
        {
            if (Uri.TryCreate(attribute.Source, UriKind.RelativeOrAbsolute, out var dataSchemaUri))
            {
                message.Source = dataSchemaUri;
            }
            else
            {
                Logger.InvalidDataSchema(logger, attribute.Source);
            }
        }

        if (!string.IsNullOrEmpty(attribute.SpecVersion) && string.IsNullOrEmpty(message.SpecVersion))
        {
            message.SpecVersion = attribute.SpecVersion!;
        }

        if (!string.IsNullOrEmpty(attribute.Subject) && message.Subject == null)
        {
            message.Subject = attribute.Subject;
        }

        if (!string.IsNullOrEmpty(attribute.Type) && message.Type == null)
        {
            message.Type = attribute.Type;
        }
    }


    private static void Apply(Message message, IPublication publication)
    {
        message.ContentType ??= publication.DefaultContentType;
        message.DataSchema ??= publication.DefaultDataSchema;
        message.ReplyTo ??= publication.DefaultReplyTo;
        message.Subject ??= publication.DefaultSubject;
        message.Source ??= publication.DefaultSource;
        message.SpecVersion ??= publication.DefaultSpecVersion;
        message.Type ??= publication.DefaultType;

        foreach (var keyPairValue in publication.DefaultHeaders)
        {
            if (!message.Headers.ContainsKey(keyPairValue.Key))
            {
                message.Headers.Add(keyPairValue.Key, keyPairValue.Value);
            }
        }
    }


    private static partial class Logger
    {
        [LoggerMessage(LogLevel.Warning, "Invalid dataschem {DataSchema}")]
        public static partial void InvalidDataSchema(ILogger logger, string dataSchema);
    }
}