using System;
using System.Net.Mime;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Amanhecer.Abstractions.Extensions;
using Amanhecer.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace Amanhecer.Messaging.Transformers;

/// <summary>
/// Declares that the <see cref="SetCloudEventTransformer"/> applies to the messages mapped
/// by the message mapper type the attribute is placed on, setting the configured CloudEvents
/// attributes on them.
/// </summary>
/// <param name="order">The position of the transformer in the pipeline.</param>
public class CloudEventAttribute(int order) : TransformerAttribute<SetCloudEventTransformer>(order)
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
/// through <see cref="CloudEventAttribute"/>, and the defaults of the resolved
/// <see cref="IPublication"/>, to outgoing messages that do not already specify them.
/// </summary>
/// <param name="logger">The logger used to report invalid attribute values.</param>
public partial class SetCloudEventTransformer(ILogger<SetCloudEventTransformer> logger) : IEncodeTransformer, IDecodeTransformer
{
    /// <inheritdoc />
    public ValueTask EncodeAsync(Message message, AmanhecerContext context,
        Func<Message, AmanhecerContext, ValueTask> next)
    {
        var attribute = context.GetMetadata<CloudEventAttribute>();
        if (attribute != null)
        {
            Apply(message, attribute);
        }

        var publication = context.GetMetadata<IPublication>(MetadataName.Publication);
        if (publication != null)
        {
            Apply(message, publication);
        }

        return next(message, context);
    }
    
    /// <inheritdoc />
    public ValueTask DecodeAsync(Message message, AmanhecerContext context, Func<Message, AmanhecerContext, ValueTask> next)
    {
        var attribute = context.GetMetadata<CloudEventAttribute>();
        if (attribute != null)
        {
            Apply(message, attribute);
        }

        var subscription = context.GetMetadata<ISubscription>(MetadataName.Subscription);
        if (subscription != null)
        {
            Apply(message, subscription);
        }

        return next(message, context);
    }

    private void Apply(Message message, CloudEventAttribute attribute)
    {
        if (!string.IsNullOrEmpty(attribute.ContentType) && message.ContentType == null)
        {
            message.ContentType = new ContentType(attribute.ContentType!);
        }

        if (!string.IsNullOrEmpty(attribute.DataSchema) && message.DataSchema == null)
        {
            if (Uri.TryCreate(attribute.DataSchema, UriKind.RelativeOrAbsolute, out var dataSchemaUri))
            {
                message.DataSchema = dataSchemaUri;
            }
            else
            {
                Logger.InvalidDataSchema(logger, attribute.DataSchema ?? "null");
            }
        }

        if (!string.IsNullOrEmpty(attribute.ReplyTo) && message.ReplyTo == null)
        {
            message.ReplyTo = attribute.ReplyTo;
        }

        if (!string.IsNullOrEmpty(attribute.Source) && message.Source == Message.DefaultSource)
        {
            if (Uri.TryCreate(attribute.Source, UriKind.RelativeOrAbsolute, out var sourceUri))
            {
                message.Source = sourceUri;
            }
            else
            {
                Logger.InvalidSource(logger, attribute.Source ?? "null");
            }
        }

        if (!string.IsNullOrEmpty(attribute.SpecVersion) && message.SpecVersion == Message.DefaultSpecVersion)
        {
            message.SpecVersion = attribute.SpecVersion!;
        }

        if (!string.IsNullOrEmpty(attribute.Subject) && message.Subject == null)
        {
            message.Subject = attribute.Subject;
        }

        if (!string.IsNullOrEmpty(attribute.Type) && message.Type == Message.DefaultType)
        {
            message.Type = attribute.Type!;
        }
    }

    private static void Apply(Message message, IPublication publication)
    {
        message.ContentType ??= publication.DefaultContentType;
        message.DataSchema ??= publication.DefaultDataSchema;
        message.ReplyTo ??= publication.DefaultReplyTo;
        message.Subject ??= publication.DefaultSubject;
        if(message.Source == Message.DefaultSource && publication.DefaultSource != null)
        {
            message.Source = publication.DefaultSource;
        }
        
        if(message.SpecVersion == Message.DefaultSpecVersion  && publication.DefaultSpecVersion != null)
        {
            message.SpecVersion = publication.DefaultSpecVersion;
        }
        
        if(message.Type == Message.DefaultType && publication.DefaultType != null)
        {
            message.Type = publication.DefaultType;
        }
        

        foreach (var keyPairValue in publication.DefaultHeaders)
        {
            if (!message.Headers.ContainsKey(keyPairValue.Key))
            {
                message.Headers.Add(keyPairValue.Key, keyPairValue.Value);
            }
        }
    }

    private static void Apply(Message message, ISubscription subscription)
    {
        message.ContentType ??= subscription.DefaultContentType;
        message.DataSchema ??= subscription.DefaultDataSchema;
        message.ReplyTo ??= subscription.DefaultReplyTo;
        message.Subject ??= subscription.DefaultSubject;
        if (message.Source == Message.DefaultSource)
        {
            message.Source = subscription.DefaultSource;
        }

        if (message.SpecVersion == Message.DefaultSpecVersion)
        {
            message.SpecVersion = subscription.DefaultSpecVersion;
        }

        if (message.Type == Message.DefaultType)
        {
            message.Type = subscription.DefaultType;
        }
    }

    private static partial class Logger
    {
        [LoggerMessage(LogLevel.Warning, "Invalid data schema {DataSchema}")]
        public static partial void InvalidDataSchema(ILogger logger, string dataSchema);

        [LoggerMessage(LogLevel.Warning, "Invalid source {Source}")]
        public static partial void InvalidSource(ILogger logger, string source);
    }
}
