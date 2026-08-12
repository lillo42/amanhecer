#if NETSTANDARD || NETFRAMEWORK
namespace System.Diagnostics.CodeAnalysis;

/// <summary>
/// Indicates that the specified method requires dynamic access to code that is not referenced
/// statically, for example through reflection.
/// </summary>
[AttributeUsage(
    AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class,
    Inherited = false)]
internal sealed class RequiresUnreferencedCodeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequiresUnreferencedCodeAttribute"/> class
    /// with the specified message.
    /// </summary>
    /// <param name="message">A message that describes why the method requires unreferenced code.</param>
    public RequiresUnreferencedCodeAttribute(string message)
    {
        Message = message;
    }

    /// <summary>
    /// Gets a message that describes why the method requires unreferenced code.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets or sets an optional URL with more information about the warning.
    /// </summary>
    public string? Url { get; set; }
}
#endif
