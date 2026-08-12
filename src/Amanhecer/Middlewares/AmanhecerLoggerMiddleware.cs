using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Amanhecer.Abstractions;
using Microsoft.Extensions.Logging;

namespace Amanhecer.Middlewares;

/// <summary>
/// Declares <see cref="AmanhecerLoggerMiddleware"/> on the annotated handler class or
/// <c>HandleAsync</c> method, adding request logging to that handler's pipeline.
/// </summary>
/// <param name="order">The order in which the middleware runs within the pipeline.</param>
public class RequestLoggingAttribute(int order) : MiddlewareAttribute(order)
{
    /// <inheritdoc />
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public override Type GetMiddlewareType()
    {
        return typeof(AmanhecerLoggerMiddleware);
    }
}

/// <summary>
/// Middleware that logs the processing of each request flowing through the pipeline: an entry
/// message before dispatch, a success message after the pipeline completes, a cancellation
/// message when the request is cancelled, and the exception when processing fails.
/// </summary>
/// <param name="logger">The logger to write the request logs to.</param>
public partial class AmanhecerLoggerMiddleware(ILogger<AmanhecerLoggerMiddleware> logger) : IMiddleware
{
    /// <inheritdoc />
    public void Initialize(object? metadata)
    {
    }

    /// <summary>
    /// Logs <c>Processing</c>/<c>Processed</c>/<c>Cancelled</c>/<c>Failed</c> for the request,
    /// scoped with the routing key and request type, then invokes the rest of the pipeline.
    /// Rethrows any exception.
    /// </summary>
    /// <param name="context">The context of the pipeline being executed.</param>
    /// <param name="next">A delegate that invokes the next middleware in the pipeline.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the pipeline has finished.</returns>
    public async ValueTask ExecuteAsync(IPipelineContext context, Func<IPipelineContext, ValueTask> next)
    {
        var requestType = context.Request.GetType().FullName ?? context.Request.GetType().Name;
        using (logger.BeginScope("{RoutingKey}", context.RoutingKey))
        using (logger.BeginScope("{RequestType}", requestType))
        {
            try
            {
                Logger.Processing(logger, context.RoutingKey, requestType);

                await next(context).ConfigureAwait(context.ContinueOnCapturedContext);

                Logger.Processed(logger, context.RoutingKey, requestType);
            }
            catch (OperationCanceledException e)
            {
                Logger.Cancelled(logger, e, context.RoutingKey, requestType);
                throw;
            }
            catch (Exception e)
            {
                Logger.Failed(logger, e, context.RoutingKey, requestType);
                throw;
            }
        }
    }

    private static partial class Logger
    {
        [LoggerMessage(LogLevel.Information, "Processing {RoutingKey} request {RequestType}")]
        public static partial void Processing(ILogger logger, string routingKey, string requestType);

        [LoggerMessage(LogLevel.Information, "Processed {RoutingKey} request {RequestType}")]
        public static partial void Processed(ILogger logger, string routingKey, string requestType);

        [LoggerMessage(LogLevel.Information, "Cancelled {RoutingKey} request {RequestType}")]
        public static partial void Cancelled(ILogger logger, Exception exception, string routingKey,
            string requestType);

        [LoggerMessage(LogLevel.Error, "Failed to process {RoutingKey} request {RequestType}")]
        public static partial void Failed(ILogger logger, Exception exception, string routingKey, string requestType);
    }
}