using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Anity.Core.Runtime.Il2Cpp;

/// <summary>
/// IL2CPP runtime support for AOT compilation.
/// Provides platform-specific optimizations and workarounds for IL2CPP builds.
/// </summary>
public static class Il2CppRuntime
{
  private static bool _isIl2Cpp;
  private static bool _initialized;

  /// <summary>
  /// Gets whether the current runtime is IL2CPP.
  /// </summary>
  public static bool IsIl2Cpp
  {
    get
    {
      if (!_initialized)
      {
        _isIl2Cpp = DetectIl2Cpp();
        _initialized = true;
      }

      return _isIl2Cpp;
    }
  }

  /// <summary>
  /// Gets whether the current platform supports JIT compilation.
  /// </summary>
  public static bool SupportsJit => !IsIl2Cpp || !RuntimeInformation.IsOSPlatform(OSPlatform.Create("IOS"));

  /// <summary>
  /// Gets whether the current platform is iOS (no JIT).
  /// </summary>
  public static bool IsIos => RuntimeInformation.IsOSPlatform(OSPlatform.Create("IOS"));

  /// <summary>
  /// Gets whether the current platform is Android.
  /// </summary>
  public static bool IsAndroid => RuntimeInformation.IsOSPlatform(OSPlatform.Android);

  /// <summary>
  /// Gets whether the current platform is WebGL (no JIT, no threads).
  /// </summary>
  public static bool IsWebGL => IsBrowser();

  /// <summary>
  /// Gets the current platform type.
  /// </summary>
  public static PlatformType Platform
  {
    get
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        return PlatformType.Windows;
      if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        return PlatformType.MacOS;
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        return PlatformType.Linux;
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("IOS")))
        return PlatformType.IOS;
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Android))
        return PlatformType.Android;
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("WEBBROWSER")))
        return PlatformType.WebGL;

      return PlatformType.Unknown;
    }
  }

  /// <summary>
  /// Initializes IL2CPP runtime support.
  /// Must be called early in the application lifecycle.
  /// </summary>
  public static void Initialize()
  {
    if (_initialized)
    {
      return;
    }

    _isIl2Cpp = DetectIl2Cpp();
    _initialized = true;

    if (_isIl2Cpp)
    {
      ConfigureForIl2Cpp();
    }
  }

  /// <summary>
  /// Configures the runtime for IL2CPP builds.
  /// </summary>
  private static void ConfigureForIl2Cpp()
  {
    // Configure for AOT environments
    // - Disable reflection emit
    // - Configure code generation options
    // - Set up metadata preservation
  }

  private static bool DetectIl2Cpp()
  {
    // In a real IL2CPP environment, this would check for IL2CPP-specific indicators
    // For now, we check if we're in an AOT-compiled environment
    try
    {
      // Check for AOT indicators
      var runtimeType = Type.GetType("Mono.Runtime");
      if (runtimeType is not null)
      {
        return false; // Mono runtime, not IL2CPP
      }

      // Check for CoreCLR indicators
      var coreClrType = Type.GetType("System.Runtime.RuntimeImports");
      if (coreClrType is not null)
      {
        return false; // CoreCLR, not IL2CPP
      }

      // Default to false for non-IL2CPP environments
      return false;
    }
    catch
    {
      return false;
    }
  }

  private static bool IsBrowser()
  {
    // In a real implementation, this would check for WebGL/browser environment
    // For now, return false
    return false;
  }
}

/// <summary>
/// Platform types for Anity runtime.
/// </summary>
public enum PlatformType
{
  Unknown,
  Windows,
  MacOS,
  Linux,
  IOS,
  Android,
  WebGL
}
