using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Anity.Core.Runtime.Il2Cpp;

public enum Platform
{
  Interpreter,
  Mono,
  IL2CPP,
  WebGL
}

public static class Il2CppRuntime
{
  private static bool _initialized;
  private static Platform _currentPlatform = Platform.Mono;
  private static readonly HashSet<Type> _registeredGenericTypes = new();
  private static readonly HashSet<string> _registeredGenericMethods = new();

  public static bool IsIl2Cpp => CurrentPlatform == Platform.IL2CPP;
  public static bool IsMono => CurrentPlatform == Platform.Mono;
  public static bool IsInterpreter => CurrentPlatform == Platform.Interpreter;
  public static bool IsWebGL => CurrentPlatform == Platform.WebGL;

  public static Platform CurrentPlatform
  {
    get => _currentPlatform;
    set => _currentPlatform = value;
  }

  public static string AOTSuffix => IsIl2Cpp ? "_AOT" : string.Empty;

  public static void Initialize()
  {
    if (_initialized) return;

    try
    {
      var monoRuntime = Type.GetType("Mono.Runtime");
      if (monoRuntime != null)
      {
        _currentPlatform = Platform.Mono;
      }
      else if (IsBrowser())
      {
        _currentPlatform = Platform.WebGL;
      }
      else
      {
        _currentPlatform = Platform.Mono;
      }
    }
    catch
    {
      _currentPlatform = Platform.Mono;
    }

    _initialized = true;
  }

  public static MethodInfo? GetGenericMethod(Type type, string methodName, Type[] typeArguments, Type[] parameterTypes)
  {
    if (type == null) throw new ArgumentNullException(nameof(type));
    if (string.IsNullOrEmpty(methodName)) throw new ArgumentNullException(nameof(methodName));

    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
    foreach (var method in methods)
    {
      if (method.Name != methodName || !method.IsGenericMethodDefinition) continue;
      var parameters = method.GetParameters();
      if (parameters.Length != parameterTypes.Length) continue;

      var match = true;
      for (var i = 0; i < parameters.Length; i++)
      {
        if (parameters[i].ParameterType.IsGenericParameter) continue;
        if (parameters[i].ParameterType != parameterTypes[i])
        {
          match = false;
          break;
        }
      }

      if (match)
      {
        return method.MakeGenericMethod(typeArguments);
      }
    }

    return null;
  }

  public static MethodInfo? ImplementGeneric(Type genericTypeDefinition, Type[] typeArguments)
  {
    if (genericTypeDefinition == null) throw new ArgumentNullException(nameof(genericTypeDefinition));
    if (typeArguments == null || typeArguments.Length == 0) throw new ArgumentNullException(nameof(typeArguments));

    var constructedType = genericTypeDefinition.MakeGenericType(typeArguments);
    return constructedType.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance);
  }

  public static IntPtr GetMethodPointer(MethodInfo method)
  {
    return Il2CppApi.GetMethodPointer(method);
  }

  public static IntPtr ResolveInternalCall(string name)
  {
    return Il2CppApi.ResolveInternalCall(name);
  }

  public static IntPtr ResolvePInvoke(string libraryName, string entryPoint)
  {
    return Il2CppApi.ResolvePInvoke(libraryName, entryPoint);
  }

  public static bool IsIos =>
    Application.platform == RuntimePlatform.IPhonePlayer;
  public static bool IsAndroid =>
    Application.platform == RuntimePlatform.Android;

  public static IEnumerable<string> GetRequiredGenericTypes()
  {
    return _registeredGenericTypes.Select(t => t.AssemblyQualifiedName ?? t.FullName ?? t.Name).ToArray();
  }

  public static void RegisterGenericTypeForAOT(Type type)
  {
    if (type == null) return;
    _registeredGenericTypes.Add(type);
  }

  public static void RegisterGenericType(Type type) => RegisterGenericTypeForAOT(type);

  public static void RegisterGenericMethod(string key)
  {
    if (!string.IsNullOrEmpty(key))
      _registeredGenericMethods.Add(key);
  }

  public static void ForcePlatform(Platform platform)
  {
    _currentPlatform = platform;
    _initialized = true;
  }

  public static int RegisteredGenericTypeCount => _registeredGenericTypes.Count;
  public static int RegisteredGenericMethodCount => _registeredGenericMethods.Count;

  public static void ClearAotRegistry()
  {
    _registeredGenericTypes.Clear();
    _registeredGenericMethods.Clear();
  }

  /// <summary>Simulate IL2CPP player process (tests / CLI -batchmode -il2cpp).</summary>
  public static void EnterIl2CppPlayerMode()
  {
    ForcePlatform(Platform.IL2CPP);
    Initialize();
  }

  public static void EnsureGenericMethod<T>(Func<T> method)
  {
    if (method == null) return;
    var key = $"{method.Method.DeclaringType?.FullName}::{method.Method.Name}<{typeof(T).FullName}>";
    _registeredGenericMethods.Add(key);
  }

  public static void EnsureGenericMethod<T1, T2>(Func<T1, T2> method)
  {
    if (method == null) return;
    var key = $"{method.Method.DeclaringType?.FullName}::{method.Method.Name}<{typeof(T1).FullName},{typeof(T2).FullName}>";
    _registeredGenericMethods.Add(key);
  }

  private static bool IsBrowser()
  {
    return RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER"));
  }
}
