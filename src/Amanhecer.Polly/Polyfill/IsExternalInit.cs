#if NETSTANDARD || NETFRAMEWORK
namespace System.Runtime.CompilerServices;

/// <summary>
/// Reserved to be used by the compiler for tracking metadata.
/// This class should not be used by developers in source code.
/// </summary>
internal sealed class IsExternalInit {}

/// <summary>
/// Specifies that a type has required members or that a member is required.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
internal sealed class RequiredMemberAttribute : Attribute {}

/// <summary>
/// Indicates that compiler support for a particular feature is required for the location where this attribute is applied.
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
internal sealed class CompilerFeatureRequiredAttribute : Attribute 
{
    /// <summary>
    /// Initialises a new instance of the <see cref="CompilerFeatureRequiredAttribute"/> class.
    /// </summary>
    /// <param name="featureName">The name of the feature to indicate.</param>
    public CompilerFeatureRequiredAttribute(string featureName) {}
}
#endif