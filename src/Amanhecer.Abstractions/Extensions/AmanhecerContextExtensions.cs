using System;

namespace Amanhecer.Abstractions.Extensions;

public static class AmanhecerContextExtensions
{
    public static void SetMetadata(this AmanhecerContext context, object? metadata)
    {
        if (metadata != null)
        {
            var metadataName = metadata.GetType().FullName ?? metadata.GetType().Name;
            context.SetMetadata(metadata, metadataName);
        }
    }

    public static void SetMetadata(this AmanhecerContext context, object? metadata, string key)
    {
        context.Metadata[key] = metadata;
    }

    public static T? GetMetadata<T>(this AmanhecerContext context)
    {
        var metadataName = typeof(T).FullName ?? typeof(T).Name;
        return context.GetMetadata<T>(metadataName);
    }

    public static T? GetMetadata<T>(this AmanhecerContext context, string key)
    {
        if (context.Metadata.TryGetValue(key, out var value) && value is T metadata)
        {
            return metadata;
        }

        return default;
    }

    public static T GetRequiredMetadata<T>(this AmanhecerContext context)
    {
        var metadataName = typeof(T).FullName ?? typeof(T).Name;
        return context.GetRequiredMetadata<T>(metadataName);
    }

    public static T GetRequiredMetadata<T>(this AmanhecerContext context, string key)
    {
        if (context.Metadata.TryGetValue(key, out var value) && value is T metadata)
        {
            return metadata;
        }

        throw new NotImplementedException();
    }
}