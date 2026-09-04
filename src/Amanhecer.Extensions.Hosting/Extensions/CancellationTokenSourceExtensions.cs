#if NETFRAMEWORK || NETSTANDARD2_0
namespace System.Threading.Tasks;

internal static class CancellationTokenSourceExtensions
{
    public static Task CancelAsync(this CancellationTokenSource cts)
    {
        cts.Cancel();
        return Task.CompletedTask;
    }
}
#endif