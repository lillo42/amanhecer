#if NETSTANDARD || NETFRAMEWORK
namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Constructor)]
internal sealed class SetsRequiredMembersAttribute : Attribute;

#endif