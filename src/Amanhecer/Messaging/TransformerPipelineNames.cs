namespace Amanhecer.Messaging;

/// <summary>
/// Builds the names of the transformer pipelines run when publishing (encode) and
/// consuming (decode) messages.
/// </summary>
internal static class TransformerPipelineNames
{
    /// <summary>
    /// Gets the name of the encode transformer pipeline of the publication with the given name.
    /// </summary>
    public static string Encode(string publicationName) =>
        $"Amanhecer.Messaging.Transformer.Encode.{publicationName}";

    /// <summary>
    /// Gets the name of the decode transformer pipeline of the subscription with the given name.
    /// </summary>
    public static string Decode(string subscriptionName) =>
        $"Amanhecer.Messaging.Transformer.Decode.{subscriptionName}";
}
