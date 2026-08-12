#if NETSTANDARD || NETFRAMEWORK
namespace System.Diagnostics.CodeAnalysis;

/// <summary>
/// Suppresses reporting of a specific rule violation, allowing multiple suppressions on a
/// single code artifact. Unlike <see cref="System.Diagnostics.CodeAnalysis.SuppressMessageAttribute"/>,
/// the suppression is not conditional on a compilation symbol.
/// </summary>
[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
internal sealed class UnconditionalSuppressMessageAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnconditionalSuppressMessageAttribute"/> class.
    /// </summary>
    /// <param name="category">The category of the rule violation.</param>
    /// <param name="checkId">The identifier of the rule to suppress.</param>
    public UnconditionalSuppressMessageAttribute(string category, string checkId)
    {
        Category = category;
        CheckId = checkId;
    }

    /// <summary>
    /// Gets the category of the rule violation.
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Gets the identifier of the rule to suppress.
    /// </summary>
    public string CheckId { get; }

    /// <summary>
    /// Gets or sets the scope of the suppression.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Gets or sets the target of the suppression.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Gets or sets the message id of the suppression.
    /// </summary>
    public string? MessageId { get; set; }

    /// <summary>
    /// Gets or sets the justification for the suppression.
    /// </summary>
    public string? Justification { get; set; }
}
#endif
