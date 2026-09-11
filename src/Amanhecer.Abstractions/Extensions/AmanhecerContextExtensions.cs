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
    /// </summary>
    /// <param name="context">The context to store the metadata in.</param>
    /// <param name="metadata">The metadata to store.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="metadata"/> is null.</exception>
    public static void SetMetadata(this AmanhecerContext context, object? metadata)
    {
        if (metadata == null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        context.SetMetadata(metadata, GetMetadataName(metadata.GetType()));
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
    /// Retrieves the metadata stored under the full name of <typeparamref name="T"/>, falling
    /// back to the first stored value assignable to <typeparamref name="T"/> when no exact
    /// entry exists (so values stored by runtime type are also found through the interfaces
    /// and base types they implement). Returns the default value when nothing matches.
    /// </summary>
    /// <typeparam name="T">The type of the metadata to retrieve.</typeparam>
    /// <param name="context">The context to retrieve the metadata from.</param>
    /// <returns>The metadata, or the default value of <typeparamref name="T"/> when absent.</returns>
    public static T? GetMetadata<T>(this AmanhecerContext context)
    {
        if (context.Metadata.TryGetValue(GetMetadataName(typeof(T)), out var exact) && exact is T exactMetadata)
        {
            return exactMetadata;
        }

        foreach (var value in context.Metadata.Values)
        {
            if (value is T metadata)
            {
                return metadata;
            }
        }

        return default;
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
    /// Retrieves the metadata stored under the full name of <typeparamref name="T"/>, falling
    /// back to the first stored value assignable to <typeparamref name="T"/> when no exact
    /// entry exists (so values stored by runtime type are also found through the interfaces
    /// and base types they implement).
    /// </summary>
    /// <typeparam name="T">The type of the metadata to retrieve.</typeparam>
    /// <param name="context">The context to retrieve the metadata from.</param>
    /// <returns>The metadata.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no matching entry exists.</exception>
    public static T GetRequiredMetadata<T>(this AmanhecerContext context)
    {
        if (context.GetMetadata<T>() is { } metadata)
        {
            return metadata;
        }

        throw new KeyNotFoundException($"The metadata '{GetMetadataName(typeof(T))}' was not found in the context.");
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

    private static string GetMetadataName(Type type)
    {
        return type.FullName ?? type.Name;
    }
}