using System;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace Amanhecer.Messaging.Transformers;

/// <summary>
/// Declares that the <see cref="CloudeventTransformer"/> applies to the handler method or
/// class the attribute is placed on, setting the configured CloudEvents attributes on the
/// outgoing messages.
/// </summary>
/// <param name="order">The position of the transformer in the pipeline.</param>
public class CloudeventAttribute(int order) : TransformeAttribute<CloudeventTransformer>(order)
{
    /// <summary>
    /// Gets or sets the content type set on messages that do not specify one.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets the schema the message payload conforms to (the CloudEvents
    /// <c>dataschema</c> attribute), set on messages that do not specify one.
    /// </summary>
    public string? DataSchema { get; set; }

    /// <summary>
    /// Gets or sets the address replies should be sent to, set on messages that do not
    /// specify one.
    /// </summary>
    public string? ReplyTo { get; set; }

    /// <summary>
    /// Gets or sets the source (the CloudEvents <c>source</c> attribute) set on messages
    /// that do not specify one.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the CloudEvents spec version set on messages that do not specify one.
    /// </summary>
    public string? SpecVersion { get; set; }

    /// <summary>
    /// Gets or sets the subject (the CloudEvents <c>subject</c> attribute) set on messages
    /// that do not specify one.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Gets or sets the type (the CloudEvents <c>type</c> attribute) set on messages that
    /// do not specify one.
    /// </summary>
    public string? Type { get; set; }
}

/// <summary>
/// An <see cref="IEncodeTransformer"/> that applies the CloudEvents attributes configured
/// through <see cref="CloudeventAttribute"/>, and the defaults of the resolved
/// <see cref="IPublication"/>, to outgoing messages that do not already specify them.
/// </summary>
/// <param name="logger">The logger used to report invalid attribute values.</param>
public partial class CloudeventTransformer(ILogger<CloudeventTransformer> logger) : IEncodeTransformer
{
    private CloudeventAttribute? _attribute;

    /// <inheritdoc />
    public void Initialize(object? metadata)
    {
        if (metadata is CloudeventAttribute attribute)
        {
            _attribute = attribute;
        }
    }

    /// <inheritdoc />
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