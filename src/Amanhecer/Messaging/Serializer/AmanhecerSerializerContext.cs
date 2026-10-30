using System.Text.Json;
using System.Text.Json.Serialization;
using Amanhecer.Messaging.Inboxes;
using Amanhecer.Messaging.Serializer;

namespace Amanhecer.Messaging;

/// <summary>
/// Source-generated JSON serializer context used to serialize messages persisted in an
/// inbox or outbox store, without requiring runtime code generation.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Always,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.Unspecified)]
[JsonSerializable(typeof(StoreMessage))]
public partial class AmanhecerSerializerContext : JsonSerializerContext;