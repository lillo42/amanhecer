using System;

namespace Amanhencer.Abstractions;

public static class Uuid
{
    public static Guid NewGuid()
    {
#if NET9_0_OR_GREATER
        return Guid.CreateVersion7();
#else
        return Guid.NewGuid();
#endif
    }
}