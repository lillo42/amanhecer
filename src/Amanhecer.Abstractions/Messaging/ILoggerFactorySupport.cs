using Microsoft.Extensions.Logging;

namespace Amanhecer.Abstractions.Messaging;

/// <summary>
/// Implemented by gateways that accept an <see cref="ILoggerFactory"/> used to create the
/// loggers of the producers and consumers they create. When the gateway is registered via
/// the Amanhecer service-collection extensions, the logger factory is set automatically
/// from the application's service provider.
/// </summary>
public interface ILoggerFactorySupport
{
    /// <summary>
    /// Gets or sets the logger factory used to create the loggers of the producers and
    /// consumers created by the gateway. When null, logging is disabled.
    /// </summary>
    ILoggerFactory? LoggerFactory { get; set; }
}
