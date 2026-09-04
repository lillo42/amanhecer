using System.Threading;
using System.Threading.Tasks;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// 
/// </summary>
public interface IMessagePumper
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="consumer"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task ExecuteAsync(IConsumer consumer, CancellationToken cancellationToken = default);
}