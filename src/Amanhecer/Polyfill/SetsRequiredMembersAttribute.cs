#if NETSTANDARD || NETFRAMEWORK
namespace System.Diagnostics.CodeAnalysis;

/// <summary>
/// Polyfill for the attribute that marks a constructor as setting all required members.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor)]
internal sealed class SetsRequiredMembersAttribute : Attribute;

#endif