using System;

namespace Amanhencer.Abstractions;

/// <summary>
/// Provides helpers for generating unique identifiers used across Amanhencer.
/// </summary>
public static class Uuid
{
    /// <summary>
    /// Creates a new <see cref="Guid"/>. On .NET 9 or later a time-ordered version 7 UUID is
    /// generated; otherwise a random UUID is returned.
    /// </summary>
    /// <returns>A newly generated <see cref="Guid"/>.</returns>
    public static Guid NewGuid()
    {
#if NET9_0_OR_GREATER
        return Guid.CreateVersion7();
#else
        return Guid.NewGuid();
#endif
    }
}