using System;
using System.Collections.Generic;

#if !NETFRAMEWORK
using System.Linq;
#endif

namespace Amanhecer.Extensions;

internal static class EnumerableExtensions
{
    public static IEnumerable<T> AppendRange<T>(this IEnumerable<T> source, IEnumerable<T> other)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (other == null)
        {
            throw new ArgumentNullException(nameof(other));
        }

#if NETFRAMEWORK
        foreach (var item in source)
        {
            yield return item;
        }

        foreach (var item in other)
        {
            yield return item;
        }
#else

        return other.Aggregate(source, (current, item) => current.Append(item));
#endif
    }
}