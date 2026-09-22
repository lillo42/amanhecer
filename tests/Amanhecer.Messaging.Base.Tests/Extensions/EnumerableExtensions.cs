using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Amanhecer.Messaging.Base.Tests.Extensions;

internal static class EnumerableExtensions
{
    public static async Task EachAsync<T>(this IEnumerable<T> source, Func<T, Task> action)
    {
        foreach (var item in source)
        {
            await action(item);
        }
    }
    
    public static void Each<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source)
        {
            action(item);
        }
    }
}