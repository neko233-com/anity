// Polyfill for RequiredMemberAttribute to support C# 11+ required members on .NET Standard 2.1
// This file can be removed when targeting .NET 7+ or later
#if !NET7_0_OR_GREATER
// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute
    {
    }
}
#endif