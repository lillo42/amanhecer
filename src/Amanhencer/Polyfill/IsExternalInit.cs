#if NETSTANDARD || NETFRAMEWORK
namespace System.Runtime.CompilerServices;

internal sealed class IsExternalInit {}
    
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
internal sealed class RequiredMemberAttribute : Attribute {}

[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
internal sealed class CompilerFeatureRequiredAttribute : Attribute 
{
    public CompilerFeatureRequiredAttribute(string featureName) {}
}
#endif