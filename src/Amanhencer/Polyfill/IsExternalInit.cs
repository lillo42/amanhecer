#if NETSTANDARD || NETFRAMEWORK
namespace System.Runtime.CompilerServices;

/// <summary>
/// Polyfill that enables init-only properties on target frameworks that do not ship it.
/// </summary>
internal sealed class IsExternalInit {}
    
/// <summary>
/// Polyfill for the attribute that marks a member as required.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
internal sealed class RequiredMemberAttribute : Attribute {}

/// <summary>
/// Polyfill for the attribute that indicates a compiler feature is required to consume a member.
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
internal sealed class CompilerFeatureRequiredAttribute : Attribute 
{
    /// <summary>
    /// Creates the attribute for the given compiler feature.
    /// </summary>
    /// <param name="featureName">The name of the required compiler feature.</param>
    public CompilerFeatureRequiredAttribute(string featureName) {}
}
#endif