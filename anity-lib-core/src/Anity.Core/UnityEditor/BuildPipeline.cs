using System;
using System.Collections.Generic;
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
    var report = new BuildReport
    {
      summary =
      {
        result = BuildResult.Succeeded,
        outputPath = NormalizePath(buildPlayerOptions.locationPathName),
        totalSize = 0L,
        totalTime = TimeSpan.Zero,
        totalErrors = 0,
        totalWarnings = 0
      }
    };

    if (buildPlayerOptions.scenes is null || buildPlayerOptions.scenes.Length == 0)
    {
      report.summary.result = BuildResult.Failed;
      report.summary.totalErrors = 1;
    }

    _lastBuildReports[buildPlayerOptions.locationPathName ?? string.Empty] = report;
    return report;
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

  public static int GetBuildTargetGroup(BuildTarget target)
  {
    return target switch
    {
      BuildTarget.StandaloneWindows64 or BuildTarget.StandaloneWindows => 1,
      BuildTarget.StandaloneLinux64 => 6,
      BuildTarget.StandaloneOSX => 1,
      BuildTarget.Android => 3,
      BuildTarget.iOS => 2,
      BuildTarget.WebGL => 4,
      _ => 0
    };
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
  NoTarget = 0,
  StandaloneWindows64 = 19,
  StandaloneWindows = 13,
  StandaloneLinux64 = 17,
  StandaloneOSX = 9,
  Android = 13 + 1,
  iOS = 9 + 1,
  WebGL = 20,
  WSAPlayer = 24,
  LinuxHeadlessSimulation = 30,
  PS5 = 38,
  GameCoreXboxSeries = 39,
  GameCoreXboxOne = 38,
  EmbeddedLinux = 57,
  NintendoSwitch = 32
}

public enum BuildTargetGroup
{
  Unknown = 0,
  Standalone = 1,
  iOS = 2,
  Android = 3,
  WebGL = 4,
  WindowsStoreApps = 5,
  LinuxStandalone = 6,
  PS4 = 25,
  PS5 = 38,
  XboxOne = 27,
  GameCoreXboxSeries = 39,
  GameCoreXboxOne = 38,
  NintendoSwitch = 32,
  Lumin = 35,
  SamsungTV = 63,
  EmbeddedLinux = 57
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
