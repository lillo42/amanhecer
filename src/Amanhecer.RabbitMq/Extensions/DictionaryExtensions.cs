using System.Collections.Generic;

namespace System.Linq;

internal static class DictionaryExtensions
{
    public static T? GetOrDefault<T>(this IReadOnlyDictionary<string, object> dictionary, string key)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        if (dictionary.TryGetValue(key, out var value) && value is T result)
        {
            return result;
        }

        return default;
#else
        var result= dictionary.GetValueOrDefault(key);
        if (result is T value)
        {
            return value;
        }
        
        return default;
#endif
    }
}