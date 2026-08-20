using System;
using System.Diagnostics.CodeAnalysis;

namespace Amanhecer.Abstractions;

/// <summary>
/// Base attribute that declares a middleware to be included in the pipeline of the annotated
/// handler class or handler method.
/// </summary>
/// <param name="order">The order in which the middleware runs within the pipeline.</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public abstract class MiddlewareAttribute(int order) : Attribute
{
    /// <summary>
    /// Gets or sets the order in which the middleware runs within the pipeline.
    /// Lower values execute earlier.
    /// </summary>
    public int Order { get; set; } = order;

    /// <summary>
    /// Returns the <see cref="Type"/> of the middleware to add to the pipeline.
    /// </summary>
    /// <returns>The type of the middleware, which must implement <see cref="IMiddleware"/>.</returns>
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public abstract Type GetMiddlewareType();
}

public abstract class MiddlewareAttribute<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    TMiddleware>(int order)
    : MiddlewareAttribute(order)
    where TMiddleware : IMiddleware
{
    
    /// <inheritdoc />
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public override Type GetMiddlewareType()
    {
        return typeof(TMiddleware);
    }
}