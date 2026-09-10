using System;
using System.Collections.Generic;

namespace Amanhecer.Abstractions.Extensions;

/// <summary>
/// Extension methods to store metadata in and retrieve metadata from an <see cref="AmanhecerContext"/>.
/// </summary>
public static class AmanhecerContextExtensions
{
    /// <summary>
    /// Stores the metadata in the context, keyed by the full name of its runtime type.
    /// <see cref="Type"/> values are keyed by <c>typeof(Type).FullName</c> so they can be
    /// retrieved with <see cref="GetMetadata{T}(AmanhecerContext)"/> as <c>GetMetadata&lt;Type&gt;()</c>.
    /// </summary>
    /// <param name="context">The context to store the metadata in.</param>
    /// <param name="metadata">The metadata to store; ignored when null.</param>
    public static void SetMetadata(this AmanhecerContext context, object? metadata)
    {
        if (metadata == null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        var metadataName = metadata.GetType().FullName ?? metadata.GetType().Name;
        context.SetMetadata(metadata, metadataName);
    }

    /// <summary>
    /// Stores the metadata in the context under the given key.
    /// </summary>
    /// <param name="context">The context to store the metadata in.</param>
    /// <param name="metadata">The metadata to store.</param>
    /// <param name="key">The key to store the metadata under.</param>
    public static void SetMetadata(this AmanhecerContext context, object? metadata, string key)
    {
        context.Metadata[key] = metadata;
    }

    /// <summary>
    /// Retrieves the metadata stored under the full name of <typeparamref name="T"/>, or the
    /// default value when no matching entry exists.
    /// </summary>
    /// <typeparam name="T">The type of the metadata to retrieve.</typeparam>
    /// <param name="context">The context to retrieve the metadata from.</param>
    /// <returns>The metadata, or the default value of <typeparamref name="T"/> when absent.</returns>
    public static T? GetMetadata<T>(this AmanhecerContext context)
    {
        var metadataName = typeof(T).FullName ?? typeof(T).Name;
        return context.GetMetadata<T>(metadataName);
    }

    /// <summary>
    /// Retrieves the metadata stored under the given key, or the default value when no matching
    /// entry exists.
    /// </summary>
    /// <typeparam name="T">The type of the metadata to retrieve.</typeparam>
    /// <param name="context">The context to retrieve the metadata from.</param>
    /// <param name="key">The key the metadata was stored under.</param>
    /// <returns>The metadata, or the default value of <typeparamref name="T"/> when absent.</returns>
    public static T? GetMetadata<T>(this AmanhecerContext context, string key)
    {
        if (context.Metadata.TryGetValue(key, out var value) && value is T metadata)
        {
            return metadata;
        }

        return default;
    }

    /// <summary>
    /// Retrieves the metadata stored under the full name of <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the metadata to retrieve.</typeparam>
    /// <param name="context">The context to retrieve the metadata from.</param>
    /// <returns>The metadata.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no matching entry exists.</exception>
    public static T GetRequiredMetadata<T>(this AmanhecerContext context)
    {
        var metadataName = typeof(T).FullName ?? typeof(T).Name;
        return context.GetRequiredMetadata<T>(metadataName);
    }

    /// <summary>
    /// Retrieves the metadata stored under the given key.
    /// </summary>
    /// <typeparam name="T">The type of the metadata to retrieve.</typeparam>
    /// <param name="context">The context to retrieve the metadata from.</param>
    /// <param name="key">The key the metadata was stored under.</param>
    /// <returns>The metadata.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no entry exists under <paramref name="key"/>.</exception>
    public static T GetRequiredMetadata<T>(this AmanhecerContext context, string key)
    {
        if (context.Metadata.TryGetValue(key, out var value) && value is T metadata)
        {
            return metadata;
        }

        throw new KeyNotFoundException($"The metadata '{key}' was not found in the context.");
    }
}