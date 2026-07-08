using System;

namespace Anity.Core.Runtime.Platform;

/// <summary>
/// Platform-specific build configuration for IL2CPP/AOT environments.
/// </summary>
public sealed class PlatformConfig
{
  /// <summary>
  /// Gets or sets the target platform.
  /// </summary>
  public Anity.Core.Runtime.Il2Cpp.PlatformType TargetPlatform { get; set; } = Anity.Core.Runtime.Il2Cpp.PlatformType.Windows;

  /// <summary>
  /// Gets or sets whether IL2CPP is enabled.
  /// </summary>
  public bool Il2CppEnabled { get; set; }

  /// <summary>
  /// Gets or sets whether AOT compilation is enabled.
  /// </summary>
  public bool AotEnabled { get; set; }

  /// <summary>
  /// Gets or sets whether code stripping is enabled.
  /// </summary>
  public bool CodeStrippingEnabled { get; set; } = true;

  /// <summary>
  /// Gets or sets whether managed code stripping is enabled.
  /// </summary>
  public bool ManagedCodeStrippingEnabled { get; set; } = true;

  /// <summary>
  /// Gets or sets the stripping level (0-3).
  /// </summary>
  public int StrippingLevel { get; set; } = 1;

  /// <summary>
  /// Gets or sets whether to preserve metadata for reflection.
  /// </summary>
  public bool PreserveMetadata { get; set; } = true;

  /// <summary>
  /// Gets or sets whether to enable incremental compilation.
  /// </summary>
  public bool IncrementalCompilation { get; set; }

  /// <summary>
  /// Gets or sets the code generation level.
  /// </summary>
  public CodeGenerationLevel CodeGenerationLevel { get; set; } = CodeGenerationLevel.LegacyJIT;

  /// <summary>
  /// Gets or sets whether to enable script debugging.
  /// </summary>
  public bool ScriptDebugging { get; set; }

  /// <summary>
  /// Gets or sets whether to enable managed debugging.
  /// </summary>
  public bool ManagedDebugging { get; set; }

  /// <summary>
  /// Gets or sets the script optimization level.
  /// </summary>
  public ScriptOptimizationLevel OptimizationLevel { get; set; } = ScriptOptimizationLevel.Balanced;

  /// <summary>
  /// Gets or sets the scripting backend.
  /// </summary>
  public ScriptingBackend ScriptingBackend { get; set; } = ScriptingBackend.IL2CPP;

  /// <summary>
  /// Gets or sets the API compatibility level.
  /// </summary>
  public ApiCompatibilityLevel ApiCompatibilityLevel { get; set; } = ApiCompatibilityLevel.NETStandard21;

  /// <summary>
  /// Gets or sets whether to enable IL2CPP code generation.
  /// </summary>
  public bool Il2CppCodeGeneration { get; set; } = true;

  /// <summary>
  /// Gets or sets whether to enable IL2CPP stack traces.
  /// </summary>
  public bool Il2CppStackTrace { get; set; } = true;

  /// <summary>
  /// Gets or sets whether to enable IL2CPP garbage collection.
  /// </summary>
  public bool Il2CppGarbageCollection { get; set; } = true;

  /// <summary>
  /// Gets or sets the IL2CPP garbage collection mode.
  /// </summary>
  public Il2CppGarbageCollectionMode Il2CppGarbageCollectionMode { get; set; } = Il2CppGarbageCollectionMode.Incremental;

  /// <summary>
  /// Gets or sets whether to enable IL2CPP managed code generation.
  /// </summary>
  public bool Il2CppManagedCodeGeneration { get; set; } = true;

  /// <summary>
  /// Gets or sets whether to enable IL2CPP managed code stripping.
  /// </summary>
  public bool Il2CppManagedCodeStripping { get; set; } = true;

  /// <summary>
  /// Gets or sets the IL2CPP managed code stripping level.
  /// </summary>
  public Il2CppManagedStrippingLevel Il2CppManagedStrippingLevel { get; set; } = Il2CppManagedStrippingLevel.Low;

  /// <summary>
  /// Gets or sets whether to enable IL2CPP metadata generation.
  /// </summary>
  public bool Il2CppMetadataGeneration { get; set; } = true;

  /// <summary>
  /// Gets or sets whether to enable IL2CPP metadata stripping.
  /// </summary>
  public bool Il2CppMetadataStripping { get; set; }

  /// <summary>
  /// Gets or sets the IL2CPP metadata stripping level.
  /// </summary>
  public Il2CppMetadataStrippingLevel Il2CppMetadataStrippingLevel { get; set; } = Il2CppMetadataStrippingLevel.Low;

  /// <summary>
  /// Creates a default configuration for the specified platform.
  /// </summary>
  public static PlatformConfig CreateDefault(Anity.Core.Runtime.Il2Cpp.PlatformType platform)
  {
    return platform switch
    {
      Anity.Core.Runtime.Il2Cpp.PlatformType.IOS => new PlatformConfig
      {
        TargetPlatform = Anity.Core.Runtime.Il2Cpp.PlatformType.IOS,
        Il2CppEnabled = true,
        AotEnabled = true,
        ScriptingBackend = ScriptingBackend.IL2CPP,
        CodeGenerationLevel = CodeGenerationLevel.Low,
        OptimizationLevel = ScriptOptimizationLevel.High,
        Il2CppManagedStrippingLevel = Il2CppManagedStrippingLevel.High
      },
      Anity.Core.Runtime.Il2Cpp.PlatformType.Android => new PlatformConfig
      {
        TargetPlatform = Anity.Core.Runtime.Il2Cpp.PlatformType.Android,
        Il2CppEnabled = true,
        AotEnabled = true,
        ScriptingBackend = ScriptingBackend.IL2CPP,
        CodeGenerationLevel = CodeGenerationLevel.Medium,
        OptimizationLevel = ScriptOptimizationLevel.Balanced,
        Il2CppManagedStrippingLevel = Il2CppManagedStrippingLevel.Medium
      },
      Anity.Core.Runtime.Il2Cpp.PlatformType.WebGL => new PlatformConfig
      {
        TargetPlatform = Anity.Core.Runtime.Il2Cpp.PlatformType.WebGL,
        Il2CppEnabled = false,
        AotEnabled = true,
        ScriptingBackend = ScriptingBackend.IL2CPP,
        CodeGenerationLevel = CodeGenerationLevel.Low,
        OptimizationLevel = ScriptOptimizationLevel.High,
        Il2CppManagedStrippingLevel = Il2CppManagedStrippingLevel.High
      },
      _ => new PlatformConfig
      {
        TargetPlatform = platform,
        Il2CppEnabled = false,
        AotEnabled = false,
        ScriptingBackend = ScriptingBackend.Mono,
        CodeGenerationLevel = CodeGenerationLevel.LegacyJIT,
        OptimizationLevel = ScriptOptimizationLevel.Balanced
      }
    };
  }
}

/// <summary>
/// Code generation levels for IL2CPP.
/// </summary>
public enum CodeGenerationLevel
{
  /// <summary>
  /// Minimal code generation for fast compilation.
  /// </summary>
  Low,

  /// <summary>
  /// Balanced code generation for development.
  /// </summary>
  Medium,

  /// <summary>
  /// Full code generation for release builds.
  /// </summary>
  High,

  /// <summary>
  /// Legacy JIT code generation.
  /// </summary>
  LegacyJIT
}

/// <summary>
/// Script optimization levels.
/// </summary>
public enum ScriptOptimizationLevel
{
  /// <summary>
  /// No optimization for fast compilation.
  /// </summary>
  FastCompilation,

  /// <summary>
  /// Balanced optimization for development.
  /// </summary>
  Balanced,

  /// <summary>
  /// Full optimization for release builds.
  /// </summary>
  High
}

/// <summary>
/// Scripting backends.
/// </summary>
public enum ScriptingBackend
{
  /// <summary>
  /// Mono scripting backend.
  /// </summary>
  Mono,

  /// <summary>
  /// IL2CPP scripting backend.
  /// </summary>
  IL2CPP
}

/// <summary>
/// API compatibility levels.
/// </summary>
public enum ApiCompatibilityLevel
{
  /// <summary>
  /// .NET Standard 2.0 compatibility.
  /// </summary>
  NETStandard20,

  /// <summary>
  /// .NET Standard 2.1 compatibility.
  /// </summary>
  NETStandard21,

  /// <summary>
  /// .NET Framework compatibility.
  /// </summary>
  NETFramework,

  /// <summary>
  /// .NET compatibility.
  /// </summary>
  NET
}

/// <summary>
/// IL2CPP garbage collection modes.
/// </summary>
public enum Il2CppGarbageCollectionMode
{
  /// <summary>
  /// Incremental garbage collection.
  /// </summary>
  Incremental,

  /// <summary>
  /// Non-incremental garbage collection.
  /// </summary>
  NonIncremental,

  /// <summary>
  /// No garbage collection.
  /// </summary>
  None
}

/// <summary>
/// IL2CPP managed code stripping levels.
/// </summary>
public enum Il2CppManagedStrippingLevel
{
  /// <summary>
  /// No stripping.
  /// </summary>
  Disabled,

  /// <summary>
  /// Minimal stripping.
  /// </summary>
  Low,

  /// <summary>
  /// Medium stripping.
  /// </summary>
  Medium,

  /// <summary>
  /// High stripping.
  /// </summary>
  High,

  /// <summary>
  /// Maximum stripping.
  /// </summary>
  Highest
}

/// <summary>
/// IL2CPP metadata stripping levels.
/// </summary>
public enum Il2CppMetadataStrippingLevel
{
  /// <summary>
  /// No stripping.
  /// </summary>
  Disabled,

  /// <summary>
  /// Minimal stripping.
  /// </summary>
  Low,

  /// <summary>
  /// Medium stripping.
  /// </summary>
  Medium,

  /// <summary>
  /// High stripping.
  /// </summary>
  High,

  /// <summary>
  /// Maximum stripping.
  /// </summary>
  Highest
}
