using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor;

public static class PlayerSettings
{
  private static readonly Dictionary<BuildTargetGroup, string> _defineSymbols = new()
  {
    [BuildTargetGroup.Standalone] = string.Empty
  };

  private static readonly Dictionary<BuildTargetGroup, ScriptingImplementation> _scriptingBackend = new()
  {
    [BuildTargetGroup.Standalone] = ScriptingImplementation.Mono2x
  };

  public static string productName { get; set; } = "Anity Product";
  public static string companyName { get; set; } = "Anity";
  public static string bundleVersion { get; set; } = "0.1.0";
  public static string applicationIdentifier { get; set; } = "com.anity.product";
  public static string bundleIdentifier
  {
    get => applicationIdentifier;
    set => applicationIdentifier = value;
  }

  public static BuildTarget defaultIsNativePlatform { get; set; } = BuildTarget.StandaloneWindows64;
  public static BuildTargetGroup buildTargetGroup
  {
    get => BuildTargetGroup.Standalone;
    set { }
  }

  public static bool runInBackground { get; set; } = true;
  public static bool stripEngineCode { get; set; } = false;
  public static bool usePlayerLog { get; set; } = false;
  public static bool enableInternalProfiler { get; set; } = false;
  public static bool fullScreenMode { get; set; } = true;
  public static int defaultScreenWidth { get; set; } = 1920;
  public static int defaultScreenHeight { get; set; } = 1080;
  public static int defaultScreenWidthMac { get; set; } = 1280;
  public static int defaultScreenHeightMac { get; set; } = 720;
  public static int defaultScreenWidthWeb { get; set; } = 960;
  public static int defaultScreenHeightWeb { get; set; } = 540;
  public static bool forceSingleInstance { get; set; } = false;
  public static bool forceSingleThreadedRendering { get; set; } = false;
  public static bool resizableWindow { get; set; } = true;
  public static string buildGUID { get; set; } = Guid.NewGuid().ToString("N");
  public static bool allowFullscreenSwitch { get; set; } = true;
  public static string scriptingRuntimeVersion { get; set; } = "Latest";
  public static bool graphicsJobs { get; set; } = false;
  public static ApiCompatibilityLevel apiCompatibilityLevel { get; set; } = ApiCompatibilityLevel.NetStandard2_0;
  public static ColorSpace colorSpace { get; set; } = ColorSpace.Linear;
  public static bool disableOldAudioSupport { get; set; }
  public static float defaultCursorHotspot { get; set; } = 0;

  public static void SetScriptingDefineSymbolsForGroup(BuildTargetGroup targetGroup, string? value)
  {
    _defineSymbols[targetGroup] = value ?? string.Empty;
  }

  public static string GetScriptingDefineSymbolsForGroup(BuildTargetGroup targetGroup)
  {
    return _defineSymbols.TryGetValue(targetGroup, out var value) ? value : string.Empty;
  }

  public static string[] GetScriptingDefineSymbolsForGroupAsArray(BuildTargetGroup targetGroup)
  {
    var symbols = GetScriptingDefineSymbolsForGroup(targetGroup);
    if (string.IsNullOrWhiteSpace(symbols))
    {
      return Array.Empty<string>();
    }

    return symbols.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
  }

  public static void SetScriptingBackend(BuildTargetGroup targetGroup, ScriptingImplementation backend)
  {
    _scriptingBackend[targetGroup] = backend;
  }

  public static ScriptingImplementation GetScriptingBackend(BuildTargetGroup targetGroup)
  {
    return _scriptingBackend.TryGetValue(targetGroup, out var value) ? value : ScriptingImplementation.Mono2x;
  }

  public static bool TryGetScriptingBackendFromTarget(BuildTarget target, out ScriptingImplementation implementation)
  {
    if (target == BuildTarget.WebGL)
    {
      implementation = ScriptingImplementation.WebAssembly;
      return true;
    }

    if (target == BuildTarget.Android || target == BuildTarget.iOS)
    {
      implementation = ScriptingImplementation.IL2CPP;
      return true;
    }

    implementation = ScriptingImplementation.Mono2x;
    return true;
  }

  public static void WriteStringToBuildLog(string buildLogText)
  {
    _ = buildLogText;
  }

  public static void FlushCache()
  {
  }

  public static bool virtualRealitySupported { get; set; }
  public static bool m_VirtualRealitySupported { get; set; }
  public static bool virtualTexturingSupportEnabled { get; set; }
  public static string? windowsGamepadBackendHint { get; set; }

  public static StandaloneBuildSubtarget standaloneBuildSubtarget { get; set; }

  public static void SetScriptingBackend(BuildTarget target, ScriptingImplementation backend)
  {
    var group = GetBuildTargetGroup(target);
    SetScriptingBackend(group, backend);
  }

  public static ScriptingImplementation GetScriptingBackend(BuildTarget target)
  {
    var group = GetBuildTargetGroup(target);
    return GetScriptingBackend(group);
  }

  public static void SetPlatformIcons(BuildTarget target, Texture2D[] icons)
  {
    _ = target;
    _ = icons;
  }

  public static Texture2D[]? GetPlatformIcons(BuildTarget target)
  {
    _ = target;
    return null;
  }

  public static string GetDefaultPlatformScriptCompilerArgs(BuildTarget target, BuildTargetGroup group)
  {
    _ = target;
    _ = group;
    return string.Empty;
  }

  private static BuildTargetGroup GetBuildTargetGroup(BuildTarget target)
  {
    return target switch
    {
      BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64
        => BuildTargetGroup.Standalone,
      BuildTarget.Android => BuildTargetGroup.Android,
      BuildTarget.iOS => BuildTargetGroup.iOS,
      BuildTarget.WebGL => BuildTargetGroup.WebGL,
      BuildTarget.StandaloneOSX or BuildTarget.StandaloneLinux64
        => BuildTargetGroup.Standalone,
      _ => BuildTargetGroup.Standalone,
    };
  }

  public class Windows
  {
    public string? iconPath { get; set; }
    public bool createSymbolicLink { get; set; }
    public string? applicationDescription { get; set; }
    public int overrideDefaultApplicationIcon { get; set; }
    public bool visibleInBackground { get; set; }
    public bool allowUnsafeCode { get; set; }
  }

  public static void Apply()
  {
  }
}

public enum StandaloneBuildSubtarget
{
  Default,
  Server
}

public enum ScriptingImplementation
{
  Mono2x,
  IL2CPP,
  WinRTDotNET,
  Wasm,
  WebAssembly
}

public enum ApiCompatibilityLevel
{
  NET_2_0,
  NET_2_0_Subset,
  NetStandard2_0
}

public enum ColorSpace
{
  Gamma,
  Linear
}
