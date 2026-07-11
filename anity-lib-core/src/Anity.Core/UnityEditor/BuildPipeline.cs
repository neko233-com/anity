using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor.Build.Reporting;

namespace UnityEditor;

public static class BuildPipeline
{
  private static readonly Dictionary<string, BuildReport> _lastBuildReports = new(StringComparer.OrdinalIgnoreCase);

  public static BuildReport BuildPlayer(string[] scenes, string locationPathName, BuildTarget target, BuildOptions options)
  {
    return BuildPlayer(new BuildPlayerOptions
    {
      scenes = scenes,
      locationPathName = locationPathName,
      target = target,
      options = options
    });
  }

  public static BuildReport BuildPlayer(BuildPlayerOptions buildPlayerOptions)
  {
    var sw = Stopwatch.StartNew();
    var target = buildPlayerOptions.target;
    var group = buildPlayerOptions.targetGroup ?? EditorUserBuildSettings.BuildTargetToBuildTargetGroup(target);
    var ext = GetPlatformExtension(target);
    var outputPath = NormalizeOutputPath(buildPlayerOptions.locationPathName, target, ext);

    if (!string.IsNullOrEmpty(Path.GetDirectoryName(outputPath)))
      Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

    var sceneCount = buildPlayerOptions.scenes?.Length ?? 0;
    var report = new BuildReport
    {
      summary =
      {
        result = BuildResult.Succeeded,
        outputPath = outputPath,
        totalSize = (ulong)(sceneCount * 10L * 1024 * 1024),
        totalTime = TimeSpan.Zero,
        totalErrors = 0,
        totalWarnings = 0,
        platform = target,
        platformGroup = group,
        platformDefaultExtension = ext,
        buildGuid = Guid.NewGuid()
      }
    };

    if (buildPlayerOptions.scenes is null || buildPlayerOptions.scenes.Length == 0)
    {
      report.summary.result = BuildResult.Failed;
      report.summary.totalErrors = 1;
    }

    sw.Stop();
    report.summary.totalTime = sw.Elapsed;
    _lastBuildReports[buildPlayerOptions.locationPathName ?? string.Empty] = report;
    return report;
  }

  private static string GetPlatformExtension(BuildTarget target) => target switch
  {
    BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64 => ".exe",
    BuildTarget.Android => ".apk",
    BuildTarget.iOS or BuildTarget.iPhone => ".ipa",
    BuildTarget.StandaloneOSX => ".app",
    BuildTarget.StandaloneLinux64 or BuildTarget.StandaloneLinux or BuildTarget.StandaloneLinuxUniversal => ".x86_64",
    BuildTarget.WebGL => "",
    BuildTarget.WSAPlayer => ".appx",
    _ => ""
  };

  private static string NormalizeOutputPath(string path, BuildTarget target, string ext)
  {
    if (string.IsNullOrWhiteSpace(path)) return string.Empty;
    var normalized = NormalizePath(path);
    if (target == BuildTarget.WebGL || target == BuildTarget.iOS || target == BuildTarget.iPhone || target == BuildTarget.tvOS || target == BuildTarget.VisionOS) return normalized;
    if (!string.IsNullOrEmpty(ext) && !normalized.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
      normalized += ext;
    return normalized;
  }

  public static void BuildPlayer(BuildPlayerOptions options, Action<BuildReport>? callback)
  {
    var report = BuildPlayer(options);
    callback?.Invoke(report);
  }

  public static string[] CollectSourcesToDelete(string path)
  {
    if (string.IsNullOrWhiteSpace(path))
    {
      return Array.Empty<string>();
    }

    return new[] { path };
  }

  public static void BuildAssetBundles(string outputPath, AssetBundleBuild[]? builds, BuildAssetBundleOptions assetBundleOptions, BuildTarget target)
  {
    _ = outputPath;
    _ = builds;
    _ = assetBundleOptions;
    _ = target;
    if (!string.IsNullOrWhiteSpace(outputPath))
    {
      Directory.CreateDirectory(outputPath);
    }
  }

  public static BuildPlayerWindow BuildPlayerWindow => new BuildPlayerWindow();

  public static BuildReport GetBuildReport()
  {
    return new BuildReport();
  }

  public static bool IsPreloadedAssetBundleName(string assetBundleName)
  {
    _ = assetBundleName;
    return false;
  }

  public static void ClearCache()
  {
  }

  public static string[] GetAllScriptAssemblyNames()
  {
    return Array.Empty<string>();
  }

  public static BuildReport lastBuildReport
  {
    get
    {
      foreach (var report in _lastBuildReports.Values)
      {
        return report;
      }

      return new BuildReport();
    }
  }

  public static string BuildAssetBundles(string outputPath, BuildAssetBundleOptions options)
  {
    if (string.IsNullOrWhiteSpace(outputPath))
    {
      return string.Empty;
    }

    Directory.CreateDirectory(outputPath);
    return outputPath;
  }

  public static string[] GetUsedAssets(string[] searchPaths, bool includeDependencies = true)
  {
    _ = searchPaths;
    _ = includeDependencies;
    return Array.Empty<string>();
  }

  public static string[] GetDirectDependencies(string assetPath)
  {
    _ = assetPath;
    return Array.Empty<string>();
  }

  public static string[] GetAllDependencies(string assetPath)
  {
    _ = assetPath;
    return Array.Empty<string>();
  }

  public static bool IsBuildTargetSupported(BuildTarget target, BuildTargetGroup targetGroup)
  {
    _ = target;
    _ = targetGroup;
    return true;
  }

  public static bool IsSceneInBuildSettings(string scenePath)
  {
    _ = scenePath;
    return false;
  }

  public static string GetPlayingPlayerDataPath()
  {
    return string.Empty;
  }

  public static void RebuildAssetBundleDependencies(string[] assetBundleNames)
  {
    _ = assetBundleNames;
  }

  public static void BuildStreams(BuildPlayerOptions options, string[] levels, string locationPathName)
  {
    _ = options;
    _ = levels;
    _ = locationPathName;
  }

  public static int GetBuildTargetGroup(BuildTarget target)
  {
    var group = EditorUserBuildSettings.BuildTargetToBuildTargetGroup(target);
    return (int)group;
  }

  private static string NormalizePath(string path)
  {
    if (string.IsNullOrWhiteSpace(path))
    {
      return string.Empty;
    }

    return path.Replace('\\', '/');
  }
}

public struct BuildPlayerOptions
{
  public string[]? scenes;
  public string? locationPathName;
  public BuildTarget target;
  public BuildTargetGroup? targetGroup;
  public BuildOptions options;
  public string[]? subtarget;
  public string[]? extraScriptingDefines;
  public string[]? managedAssemblies;
  public string? assetBundleManifestPath;
  public string? addressablesProfileId;
  public bool? explicitReferences;
  public bool? allowDebugging;
  public bool? development;
  public bool? autoRunPlayer;
  public bool? connectWithProfiler;
  public bool? enableDeepProfilingSupport;
  public bool? enableNativePlatformBackendsForNewInputSystem;
  public bool? disableOldInputManagerSupport;
}

public struct AssetBundleBuild
{
  public string assetBundleName;
  public string[]? assetNames;
  public string[]? assetBundleVariant;
}

public enum BuildTarget
{
  NoTarget = -2,
  BB10 = -1,
  MetroPlayer = -1,
  iPhone = 9,
  StandaloneOSX = 2,
  StandaloneOSXUniversal = 2,
  StandaloneOSXIntel = 4,
  StandaloneWindows = 5,
  WebPlayer = 6,
  WebPlayerStreamed = 7,
  iOS = 9,
  StandaloneWindows64 = 19,
  OSXWebPlayer = 12,
  OSXUniversal = 2,
  OSXWebPlayerStreamed = 13,
  Android = 13,
  StandaloneLinux = 17,
  StandaloneLinux64 = 24,
  StandaloneLinuxUniversal = 17,
  WebGL = 20,
  WSAPlayer = 21,
  WiiU = 30,
  tvOS = 37,
  PSP2 = 25,
  PS4 = 31,
  PSM = 32,
  XboxOne = 33,
  N3DS = 35,
  XboxOneD3D12 = 43,
  Switch = 38,
  Lumin = 44,
  Stadia = 45,
  CloudRendering = 46,
  PS5 = 47,
  GameCoreXboxSeries = 48,
  GameCoreXboxOne = 49,
  GameCoreScarlett = 48,
  LinuxHeadlessSimulation = 50,
  EmbeddedLinux = 53,
  QNX = 57,
  VisionOS = 56
}

public enum BuildTargetGroup
{
  Unknown = 0,
  Standalone = 1,
  iOS = 4,
  AppleTV = 25,
  tvOS = 25,
  VisionOS = 39,
  Android = 7,
  WebGL = 13,
  WSA = 14,
  Metro = 14,
  WindowsStoreApps = 14,
  PS4 = 19,
  XboxOne = 21,
  Facebook = 22,
  Switch = 24,
  Lumin = 28,
  Stadia = 29,
  CloudRendering = 30,
  PS5 = 36,
  GameCoreXboxOne = 37,
  GameCoreXboxSeries = 38,
  EmbeddedLinux = 35
}

[Flags]
public enum BuildOptions : ulong
{
  None = 0,
  Development = 1 << 0,
  AutoRunPlayer = 1 << 1,
  BuildAdditionalStreamedScenes = 1 << 2,
  CompressWithLz4 = 1 << 3,
  ConnectWithProfiler = 1 << 4,
  AllowDebugging = 1 << 5,
  EnableHeadlessMode = 1 << 6,
  ShowBuiltPlayer = 1 << 7,
  CompressWithLz4HC = 1 << 8,
  DetailedBuildReport = 1 << 9,
  IncludeTestAssemblies = 1 << 10,
  WaitForManagerJobCompletion = 1 << 11,
  ScriptChangesOnly = 1 << 12,
  StrictMode = 1 << 13,
  StripEngineCode = 1 << 14,
  EnableDeepProfilingSupport = 1 << 15,
  EnableNativePlatformBackendsForNewInputSystem = 1 << 16,
  DisableOldInputManagerSupport = 1 << 17,
  ForceEnableAssertions = 1 << 18,
  AllowDebugCodeOptimization = 1 << 19,
  UseDeterministicBuild = 1 << 20,
  ForceSingleInstance = 1 << 21
}

[Flags]
public enum BuildAssetBundleOptions : ulong
{
  None = 0,
  UncompressedAssetBundle = 1 << 0,
  ChunkBasedCompression = 1 << 1,
  ForceRebuildAssetBundle = 1 << 2,
  IgnoreTypeTreeChanges = 1 << 3,
  AppendHashToAssetBundleName = 1 << 4,
  StrictMode = 1 << 5,
  DryRunBuild = 1 << 6,
  DisableWriteTypeTree = 1 << 7,
  RebuildAssetBundle = 1 << 8,
  IgnoreOverrides = 1 << 9,
  DisableLoadAssetByFileName = 1 << 10,
  DisableLoadAssetByFileNameWithExtension = 1 << 11
}

public enum BuildResult
{
  Unknown = 0,
  Succeeded = 1,
  Failed = 2,
  Cancelled = 3,
  InvalidPlayerSetup = 4
}
