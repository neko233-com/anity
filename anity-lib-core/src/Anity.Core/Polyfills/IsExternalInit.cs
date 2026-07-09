// Polyfill for IsExternalInit to support C# 9+ init accessors on .NET Standard 2.1
// This file can be removed when targeting .NET 5+ or later
#if !NET5_0_OR_GREATER
// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
#endif