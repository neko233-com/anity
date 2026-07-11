using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

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

  private static readonly Dictionary<BuildTargetGroup, GraphicsDeviceType[]> _graphicsAPIs = new()
  {
    [BuildTargetGroup.Standalone] = new[] { GraphicsDeviceType.Direct3D11, GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLCore },
    [BuildTargetGroup.iOS] = new[] { GraphicsDeviceType.Metal },
    [BuildTargetGroup.Android] = new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.OpenGLES2 },
    [BuildTargetGroup.WebGL] = new[] { GraphicsDeviceType.WebGL2, GraphicsDeviceType.OpenGLES2 }
  };

  private static readonly Dictionary<BuildTargetGroup, bool> _useDefaultGraphicsAPIs = new();

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

  public static void SetGraphicsAPIs(BuildTargetGroup targetGroup, GraphicsDeviceType[]? apis)
  {
    if (apis == null || apis.Length == 0) { _useDefaultGraphicsAPIs[targetGroup] = true; return; }
    _useDefaultGraphicsAPIs[targetGroup] = false;
    _graphicsAPIs[targetGroup] = apis;
  }

  public static GraphicsDeviceType[] GetGraphicsAPIs(BuildTargetGroup targetGroup)
  {
    if (_graphicsAPIs.TryGetValue(targetGroup, out var apis)) return apis.ToArray();
    return targetGroup switch
    {
      BuildTargetGroup.iOS => new[] { GraphicsDeviceType.Metal },
      BuildTargetGroup.Android => new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 },
      BuildTargetGroup.WebGL => new[] { GraphicsDeviceType.WebGL2 },
      _ => new[] { GraphicsDeviceType.Direct3D11 }
    };
  }

  public static bool GetUseDefaultGraphicsAPIs(BuildTargetGroup targetGroup) =>
    !_useDefaultGraphicsAPIs.TryGetValue(targetGroup, out var v) || v;

  public static bool IsMobilePlatform(BuildTarget target) => target == BuildTarget.iOS || target == BuildTarget.Android;
  public static bool IsBuildTargetSupported(BuildTargetGroup group) { _ = group; return true; }

  public static string iPhoneSplashScreen { get; set; } = "";
  public static string iOSSdkVersion { get; set; } = "Device SDK";
  public static string iOSTargetOSVersionString { get; set; } = "15.0";
  public static bool iOSRequireARKit { get; set; }
  public static bool iosShowActivityIndicatorOnLoading { get; set; } = true;
  public static int iOSScriptCallOptimization { get; set; }
  public static bool iosUseCustomAppBackgroundBehavior { get; set; }
  public static bool iosAllowHTTPDownload { get; set; } = true;
  public static string iOSURLSchemes { get; set; } = "";
  public static string iOSCameraUsageDescription { get; set; } = "";
  public static string iOSLocationUsageDescription { get; set; } = "";
  public static string iOSMicrophoneUsageDescription { get; set; } = "";

  public static AndroidSdkVersions AndroidMinSdkVersion { get; set; } = AndroidSdkVersions.AndroidApiLevel33;
  public static AndroidSdkVersions AndroidTargetSdkVersion { get; set; } = AndroidSdkVersions.AndroidApiLevel33;
  public static int AndroidBundleVersionCode { get; set; } = 1;
  public static string AndroidKeystoreName { get => _androidKeystoreName; set => _androidKeystoreName = value ?? ""; }
  private static string _androidKeystoreName = "";
  public static string AndroidKeyAliasName { get; set; } = "";
  public static bool AndroidUseAPKExpansionFiles { get; set; }
  public static bool AndroidIsGame { get; set; } = true;
  public static AndroidGamepadSupportLevel AndroidGamepadSupportLevel { get; set; } = AndroidGamepadSupportLevel.SupportsDPad;
  public static bool AndroidEnableArmv9SecurityFeatures { get; set; }
  public static AndroidArchitecture AndroidTargetArchitectures { get; set; } = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
  public static MobileTextureSubtarget AndroidTargetSubtarget { get; set; } = MobileTextureSubtarget.Generic;
  public static bool AndroidValidateAppBundleSize { get; set; } = true;

  public static string WebGLMemorySize { get; set; } = "512";
  public static WebGLCompressionFormat WebGLCompressionFormat { get; set; } = WebGLCompressionFormat.Brotli;
  public static WebGLLinkerTarget WebGLLinkerTarget { get; set; } = WebGLLinkerTarget.Wasm;
  public static bool WebGLThreadsSupport { get; set; }
  public static bool WebGLDecompressionFallback { get; set; }
  public static WebGLExceptionSupport WebGLExceptionSupport { get; set; } = WebGLExceptionSupport.None;

  public static string WSMetadataApplicationDescription { get; set; } = "";
  public static string WSHubPackageName { get; set; } = "";

  public static int defaultScreenOrientationPortrait { get; set; } = 1;
  public static UIInterfaceOrientationMask defaultInterfaceOrientation { get; set; } = UIInterfaceOrientationMask.AllButUpsideDown;
  public static bool allowedAutorotateToPortrait { get; set; } = true;
  public static bool allowedAutorotateToPortraitUpsideDown { get; set; }
  public static bool allowedAutorotateToLandscapeRight { get; set; } = true;
  public static bool allowedAutorotateToLandscapeLeft { get; set; } = true;
  public static bool defaultScreenOrientationLandscape { get; set; } = true;

  public static readonly iOSSettings iOS = new();
  public static readonly AndroidSettings Android = new();
}

public class iOSSettings
{
  public string sdkVersion { get => PlayerSettings.iOSSdkVersion; set => PlayerSettings.iOSSdkVersion = value; }
  public string targetOSVersionString { get => PlayerSettings.iOSTargetOSVersionString; set => PlayerSettings.iOSTargetOSVersionString = value; }
  public bool requireARKit { get => PlayerSettings.iOSRequireARKit; set => PlayerSettings.iOSRequireARKit = value; }
}

public class AndroidSettings
{
  public AndroidSdkVersions minSdkVersion { get => PlayerSettings.AndroidMinSdkVersion; set => PlayerSettings.AndroidMinSdkVersion = value; }
  public AndroidSdkVersions targetSdkVersion { get => PlayerSettings.AndroidTargetSdkVersion; set => PlayerSettings.AndroidTargetSdkVersion = value; }
  public int bundleVersionCode { get => PlayerSettings.AndroidBundleVersionCode; set => PlayerSettings.AndroidBundleVersionCode = value; }
  public string keystoreName { get => PlayerSettings.AndroidKeystoreName; set => PlayerSettings.AndroidKeystoreName = value; }
  public AndroidArchitecture targetArchitectures { get => PlayerSettings.AndroidTargetArchitectures; set => PlayerSettings.AndroidTargetArchitectures = value; }
  public bool isGame { get => PlayerSettings.AndroidIsGame; set => PlayerSettings.AndroidIsGame = value; }
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
