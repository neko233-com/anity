using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace Anity.Core.Runtime.Il2Cpp;

/// <summary>
/// Deep IL2CPP runtime surface (Unity il2cpp API semantics for managed tests / AOT host).
/// </summary>
public static class Il2CppApi
{
    private static readonly ConcurrentDictionary<string, IntPtr> s_Icalls = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, IntPtr> s_PInvokes = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<MethodInfo, IntPtr> s_MethodPtrs = new();
    private static long s_NextPtr = 0x10000;

    public static bool initialized => Il2CppRuntime.IsIl2Cpp || s_MethodPtrs.Count > 0;

    public static void InitializeRuntime()
    {
        Il2CppRuntime.EnterIl2CppPlayerMode();
        // Register common icalls
        RegisterInternalCall("UnityEngine.Object::GetInstanceID", () => 0);
        RegisterInternalCall("UnityEngine.Time::get_deltaTime", () => Time.deltaTime);
    }

    public static IntPtr RegisterInternalCall(string name, Delegate? d = null)
    {
        var ptr = AllocPtr();
        s_Icalls[name] = ptr;
        _ = d;
        return ptr;
    }

    public static IntPtr ResolveInternalCall(string name)
    {
        if (string.IsNullOrEmpty(name)) return IntPtr.Zero;
        if (s_Icalls.TryGetValue(name, out var p)) return p;
        // Unity returns null for missing icall → we allocate stub so AOT links
        return RegisterInternalCall(name);
    }

    public static IntPtr ResolvePInvoke(string library, string entry)
    {
        string key = (library ?? "") + "!" + (entry ?? "");
        if (s_PInvokes.TryGetValue(key, out var p)) return p;
        p = AllocPtr();
        s_PInvokes[key] = p;
        return p;
    }

    public static IntPtr GetMethodPointer(MethodInfo method)
    {
        if (method == null) return IntPtr.Zero;
        return s_MethodPtrs.GetOrAdd(method, _ => AllocPtr());
    }

    public static object? InvokeMethod(MethodInfo method, object? target, object?[]? args)
    {
        if (method == null) throw new ArgumentNullException(nameof(method));
        // Ensure pointer registered (AOT surface)
        _ = GetMethodPointer(method);
        try
        {
            return method.Invoke(target, args);
        }
        catch (TargetInvocationException ex)
        {
            throw new Il2CppException(ex.InnerException?.Message ?? ex.Message, ex.InnerException);
        }
    }

    public static void RaiseManagedException(Exception ex)
    {
        throw new Il2CppException(ex?.Message ?? "il2cpp exception", ex);
    }

    public static bool IsNativeObjectAlive(UnityEngine.Object obj)
    {
        return obj != null && !obj.Equals(null);
    }

    public static void CollectGarbage(bool block = true)
    {
        GC.Collect();
        if (block) GC.WaitForPendingFinalizers();
    }

    public static long GetUsedHeapSize()
    {
        return GC.GetTotalMemory(false);
    }

    public static IReadOnlyCollection<string> GetRegisteredIcalls() => (IReadOnlyCollection<string>)s_Icalls.Keys;

    public static void ClearRegistries()
    {
        s_Icalls.Clear();
        s_PInvokes.Clear();
        s_MethodPtrs.Clear();
    }

    private static IntPtr AllocPtr()
    {
        long v = System.Threading.Interlocked.Add(ref s_NextPtr, 8);
        return new IntPtr(v);
    }
}

public sealed class Il2CppException : Exception
{
    public Il2CppException(string message) : base(message) { }
    public Il2CppException(string message, Exception? inner) : base(message, inner) { }
}

/// <summary>
/// Stripping / AOT preserve helper used by IL2CPP build (Unity link.xml semantics).
/// </summary>
public static class Il2CppStripping
{
    public static HashSet<string> PreserveTypeFullNames { get; } = new(StringComparer.Ordinal);

    public static void Preserve(Type type)
    {
        if (type == null) return;
        PreserveTypeFullNames.Add(type.FullName ?? type.Name);
        Il2CppRuntime.RegisterGenericType(type);
    }

    public static void PreserveAssembly(Assembly asm)
    {
        if (asm == null) return;
        foreach (var t in SafeGetTypes(asm))
            Preserve(t);
    }

    public static bool IsPreserved(Type type)
    {
        if (type == null) return false;
        if (type.GetCustomAttributes(typeof(PreserveAttribute), true).Length > 0)
            return true;
        return PreserveTypeFullNames.Contains(type.FullName ?? type.Name);
    }

    public static ManagedStrippingLevel EffectiveLevel(ManagedStrippingLevel configured, bool development)
    {
        if (development && configured == ManagedStrippingLevel.High)
            return ManagedStrippingLevel.Medium;
        return configured;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly asm)
    {
        try { return asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }
}
