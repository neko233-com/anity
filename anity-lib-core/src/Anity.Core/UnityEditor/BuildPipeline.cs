using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using UnityEditor.Build.Reporting;
using UnityEngine;

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
    var outputPath = NormalizeOutputPath(buildPlayerOptions.locationPathName, target, ext, buildPlayerOptions.options);
    var acceptExternalMods = (buildPlayerOptions.options & BuildOptions.AcceptExternalModificationsToPlayer) != 0;

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

    if (target == BuildTarget.Android && report.summary.result == BuildResult.Succeeded)
    {
      var archError = ValidateAndroidArchitectures();
      if (archError != null)
      {
        report.summary.result = BuildResult.Failed;
        report.summary.totalErrors++;
        report.steps = new[]
        {
          new BuildStep { name = archError, depth = 0 }
        };
      }
      else
      {
        var buildDir = outputPath;

        if (acceptExternalMods)
        {
          Directory.CreateDirectory(buildDir);
        }
        else
        {
          if (!string.IsNullOrEmpty(Path.GetDirectoryName(buildDir)))
            Directory.CreateDirectory(Path.GetDirectoryName(buildDir)!);
        }

        var apkSize = GenerateAndroidApkStructure(buildDir, acceptExternalMods);
        report.summary.totalSize = apkSize;
        report.files = GenerateApkFileList(buildDir, acceptExternalMods);
      }
    }
    else if (report.summary.result == BuildResult.Succeeded)
    {
      if (!string.IsNullOrEmpty(Path.GetDirectoryName(outputPath)))
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    }

    sw.Stop();
    report.summary.totalTime = sw.Elapsed;
    _lastBuildReports[buildPlayerOptions.locationPathName ?? string.Empty] = report;
    return report;
  }

  private static string? ValidateAndroidArchitectures()
  {
    var archs = PlayerSettings.Android.targetArchitectures;
    if (archs == AndroidArchitecture.None)
      return "AndroidTargetArchitectures is None; select at least one architecture (ARMv7, ARM64, X86, X86_64)";
    return null;
  }

  private static ulong GenerateAndroidApkStructure(string outputPath, bool acceptExternalMods)
  {
    var tempDir = acceptExternalMods ? outputPath : Path.Combine(Path.GetTempPath(), "anity_apk_" + Guid.NewGuid().ToString("N"));
    try
    {
      if (Directory.Exists(tempDir))
        Directory.Delete(tempDir, true);
      Directory.CreateDirectory(tempDir);

      var resDir = Path.Combine(tempDir, "res");
      Directory.CreateDirectory(Path.Combine(resDir, "values"));
      Directory.CreateDirectory(Path.Combine(resDir, "layout"));
      Directory.CreateDirectory(Path.Combine(resDir, "mipmap-hdpi"));
      Directory.CreateDirectory(Path.Combine(resDir, "mipmap-xhdpi"));
      Directory.CreateDirectory(Path.Combine(resDir, "mipmap-xxhdpi"));

      var metaInfDir = Path.Combine(tempDir, "META-INF");
      Directory.CreateDirectory(metaInfDir);

      var libDir = Path.Combine(tempDir, "lib");
      var archs = PlayerSettings.Android.targetArchitectures;
      ulong nativeLibSize = 0;
      if (archs.HasFlag(AndroidArchitecture.ARMv7))
      {
        var armDir = Path.Combine(libDir, "armeabi-v7a");
        Directory.CreateDirectory(armDir);
        File.WriteAllBytes(Path.Combine(armDir, "libunity.so"), new byte[2 * 1024 * 1024]);
        nativeLibSize += 2 * 1024 * 1024;
      }
      if (archs.HasFlag(AndroidArchitecture.ARM64))
      {
        var arm64Dir = Path.Combine(libDir, "arm64-v8a");
        Directory.CreateDirectory(arm64Dir);
        File.WriteAllBytes(Path.Combine(arm64Dir, "libunity.so"), new byte[3 * 1024 * 1024]);
        nativeLibSize += 3 * 1024 * 1024;
      }
      if (archs.HasFlag(AndroidArchitecture.X86))
      {
        var x86Dir = Path.Combine(libDir, "x86");
        Directory.CreateDirectory(x86Dir);
        File.WriteAllBytes(Path.Combine(x86Dir, "libunity.so"), new byte[2 * 1024 * 1024]);
        nativeLibSize += 2 * 1024 * 1024;
      }
      if (archs.HasFlag(AndroidArchitecture.X86_64))
      {
        var x64Dir = Path.Combine(libDir, "x86_64");
        Directory.CreateDirectory(x64Dir);
        File.WriteAllBytes(Path.Combine(x64Dir, "libunity.so"), new byte[3 * 1024 * 1024]);
        nativeLibSize += 3 * 1024 * 1024;
      }

      var manifest = GenerateAndroidManifest();
      File.WriteAllText(Path.Combine(tempDir, "AndroidManifest.xml"), manifest);

      File.WriteAllBytes(Path.Combine(tempDir, "classes.dex"), new byte[512 * 1024]);
      File.WriteAllBytes(Path.Combine(tempDir, "resources.arsc"), new byte[128 * 1024]);

      File.WriteAllText(Path.Combine(metaInfDir, "MANIFEST.MF"), "Manifest-Version: 1.0\r\nCreated-By: Anity\r\n");
      File.WriteAllText(Path.Combine(metaInfDir, "CERT.SF"), "Signature-Version: 1.0\r\nCreated-By: Anity\r\n");
      File.WriteAllText(Path.Combine(metaInfDir, "CERT.RSA"), string.Empty);

      var assetsDir = Path.Combine(tempDir, "assets");
      Directory.CreateDirectory(assetsDir);
      File.WriteAllBytes(Path.Combine(assetsDir, "binData"), new byte[1024 * 1024]);

      ulong totalSize = 0;
      totalSize += (ulong)manifest.Length;
      totalSize += 512 * 1024;
      totalSize += 128 * 1024;
      totalSize += nativeLibSize;
      totalSize += 1024 * 1024;
      totalSize += 1024;

      if (!acceptExternalMods)
      {
        if (File.Exists(outputPath))
          File.Delete(outputPath);
        CreateZipFromDirectory(tempDir, outputPath);
        if (File.Exists(outputPath))
        {
          var fi = new FileInfo(outputPath);
          totalSize = (ulong)fi.Length;
        }
        Directory.Delete(tempDir, true);
      }
      else
      {
        totalSize += CalculateDirectorySize(tempDir);
      }

      return totalSize;
    }
    catch
    {
      if (Directory.Exists(tempDir) && !acceptExternalMods)
      {
        try { Directory.Delete(tempDir, true); } catch { }
      }
      return 0;
    }
  }

  private static string GenerateAndroidManifest()
  {
    var packageName = PlayerSettings.applicationIdentifier ?? "com.anity.product";
    var versionCode = PlayerSettings.Android.bundleVersionCode;
    var versionName = PlayerSettings.bundleVersion ?? "1.0";
    var minSdk = (int)PlayerSettings.Android.minSdkVersion;
    var targetSdk = (int)PlayerSettings.Android.targetSdkVersion;
    var orientation = GetAndroidOrientationString(PlayerSettings.defaultScreenOrientation);
    var isGame = PlayerSettings.Android.isGame;

    return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" package=""{packageName}"" android:versionCode=""{versionCode}"" android:versionName=""{versionName}"">
  <uses-sdk android:minSdkVersion=""{minSdk}"" android:targetSdkVersion=""{targetSdk}"" />
  <uses-feature android:glEsVersion=""0x00030000"" android:required=""true"" />
  <application android:label=""{PlayerSettings.productName}"" android:icon=""@mipmap/app_icon"" android:debuggable=""false"" android:isGame=""{(isGame ? "true" : "false")}"">
    <activity android:name=""com.unity3d.player.UnityPlayerActivity"" android:screenOrientation=""{orientation}"" android:configChanges=""orientation|screenSize|keyboardHidden|keyboard|navigation"" android:launchMode=""singleTask"">
      <intent-filter>
        <action android:name=""android.intent.action.MAIN"" />
        <category android:name=""android.intent.category.LAUNCHER"" />
      </intent-filter>
    </activity>
  </application>
</manifest>";
  }

  private static string GetAndroidOrientationString(UIOrientation orientation) => orientation switch
  {
    UIOrientation.Portrait => "portrait",
    UIOrientation.PortraitUpsideDown => "reversePortrait",
    UIOrientation.LandscapeLeft => "landscape",
    UIOrientation.LandscapeRight => "reverseLandscape",
    UIOrientation.AutoRotation => "fullSensor",
    _ => "fullSensor"
  };

  private static void CreateZipFromDirectory(string sourceDir, string zipPath)
  {
    try
    {
      if (File.Exists(zipPath))
        File.Delete(zipPath);
      System.IO.Compression.ZipFile.CreateFromDirectory(sourceDir, zipPath);
    }
    catch
    {
      using var fs = File.Create(zipPath);
      var placeholder = new byte[1024];
      fs.Write(placeholder, 0, placeholder.Length);
    }
  }

  private static ulong CalculateDirectorySize(string path)
  {
    ulong size = 0;
    try
    {
      foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
      {
        try
        {
          var fi = new FileInfo(file);
          size += (ulong)fi.Length;
        }
        catch { }
      }
    }
    catch { }
    return size;
  }

  private static BuildFile[] GenerateApkFileList(string outputPath, bool acceptExternalMods)
  {
    var files = new List<BuildFile>();
    if (acceptExternalMods)
    {
      foreach (var file in Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories))
      {
        files.Add(new BuildFile { path = file, role = Role.Output });
      }
    }
    else
    {
      files.Add(new BuildFile { path = outputPath, role = Role.Output });
    }
    return files.ToArray();
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

  private static string NormalizeOutputPath(string path, BuildTarget target, string ext, BuildOptions options)
  {
    if (string.IsNullOrWhiteSpace(path)) return string.Empty;
    var normalized = NormalizePath(path);
    bool acceptExternalMods = (options & BuildOptions.AcceptExternalModificationsToPlayer) != 0;
    if (target == BuildTarget.WebGL || target == BuildTarget.iOS || target == BuildTarget.iPhone || target == BuildTarget.tvOS || target == BuildTarget.VisionOS) return normalized;
    if (target == BuildTarget.Android && acceptExternalMods) return normalized;
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

  public static AssetBundleManifest BuildAssetBundles(string outputPath, AssetBundleBuild[]? builds, BuildAssetBundleOptions assetBundleOptions, BuildTarget target)
  {
    _ = target;
    var manifest = new AssetBundleManifest { name = "AssetBundleManifest" };
    if (string.IsNullOrWhiteSpace(outputPath))
      return manifest;

    if ((assetBundleOptions & BuildAssetBundleOptions.DryRunBuild) != 0)
    {
      if (builds != null)
      {
        foreach (var build in builds)
        {
          if (!string.IsNullOrEmpty(build.assetBundleName))
            manifest.AddBundle(build.assetBundleName, default, Array.Empty<string>());
        }
      }
      return manifest;
    }

    Directory.CreateDirectory(outputPath);
    builds ??= Array.Empty<AssetBundleBuild>();

    // Pass 1: collect names for dependency graph (path prefix → bundle)
    var pathToBundle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var build in builds)
    {
      if (string.IsNullOrEmpty(build.assetBundleName) || build.assetNames == null) continue;
      foreach (var assetPath in build.assetNames)
      {
        if (!string.IsNullOrEmpty(assetPath))
          pathToBundle[assetPath] = build.assetBundleName;
      }
    }

    foreach (var build in builds)
    {
      if (string.IsNullOrEmpty(build.assetBundleName)) continue;

      var catalog = new AssetBundleFormat.BundleCatalog
      {
        bundleName = build.assetBundleName
      };

      var names = build.assetNames ?? Array.Empty<string>();
      var depSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      foreach (var assetPath in names)
      {
        if (string.IsNullOrEmpty(assetPath)) continue;

        // Scene assets
        if (assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
        {
          catalog.scenes.Add(assetPath);
          continue;
        }

        Object? asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
        if (asset == null)
        {
          // Create placeholder text asset so pack still succeeds (StrictMode fails)
          if ((assetBundleOptions & BuildAssetBundleOptions.StrictMode) != 0)
            throw new InvalidOperationException($"Missing asset for bundle: {assetPath}");
          asset = new TextAsset($"missing:{assetPath}");
          asset.name = Path.GetFileNameWithoutExtension(assetPath);
        }

        catalog.assets.Add(AssetBundleFormat.SerializeAsset(assetPath, asset));

        // Direct dependencies via BuildPipeline.GetDirectDependencies if available
        foreach (var depPath in GetDirectDependencies(assetPath))
        {
          if (pathToBundle.TryGetValue(depPath, out var depBundle) &&
              !string.Equals(depBundle, build.assetBundleName, StringComparison.OrdinalIgnoreCase))
            depSet.Add(depBundle);
        }
      }

      catalog.dependencies.AddRange(depSet);
      catalog.hash = AssetBundleFormat.ComputeContentHash(names);
      catalog.crc = (uint)(catalog.hash.u32_0 ^ catalog.hash.u32_1);

      string fileName = build.assetBundleName;
      if ((assetBundleOptions & BuildAssetBundleOptions.AppendHashToAssetBundleName) != 0)
        fileName = $"{build.assetBundleName}_{catalog.hash}";

      if (build.assetBundleVariant != null && build.assetBundleVariant.Length > 0)
      {
        foreach (var variant in build.assetBundleVariant)
        {
          if (string.IsNullOrEmpty(variant)) continue;
          string variantName = $"{build.assetBundleName}.{variant}";
          catalog.bundleName = variantName;
          var bytesV = AssetBundleFormat.WriteBundle(catalog, assetBundleOptions);
          catalog.crc = AssetBundleFormat.ComputeCrc(bytesV);
          bytesV = AssetBundleFormat.WriteBundle(catalog, assetBundleOptions);
          bytesV = AssetBundleCompression.MaybeCompress(bytesV, assetBundleOptions);
          File.WriteAllBytes(Path.Combine(outputPath, variantName), bytesV);
          manifest.AddBundleWithVariant(build.assetBundleName, variant, catalog.hash);
          manifest.AddBundle(variantName, catalog.hash, catalog.dependencies.ToArray());
        }
      }
      else
      {
        var bytes = AssetBundleFormat.WriteBundle(catalog, assetBundleOptions);
        catalog.crc = AssetBundleFormat.ComputeCrc(bytes);
        bytes = AssetBundleFormat.WriteBundle(catalog, assetBundleOptions);
        bytes = AssetBundleCompression.MaybeCompress(bytes, assetBundleOptions);
        File.WriteAllBytes(Path.Combine(outputPath, fileName), bytes);
        manifest.AddBundle(build.assetBundleName, catalog.hash, catalog.dependencies.ToArray());
      }
    }

    // Write manifest bundle itself (Unity writes a manifest asset bundle)
    var manifestCatalog = new AssetBundleFormat.BundleCatalog
    {
      bundleName = Path.GetFileName(outputPath.TrimEnd('/', '\\')),
      hash = AssetBundleFormat.ComputeContentHash(manifest.GetAllAssetBundles()),
      assets =
      {
        AssetBundleFormat.SerializeAsset("AssetBundleManifest", manifest)
      }
    };
    var manBytes = AssetBundleFormat.WriteBundle(manifestCatalog, assetBundleOptions);
    string manName = Path.GetFileName(outputPath.TrimEnd('/', '\\'));
    if (string.IsNullOrEmpty(manName)) manName = "AssetBundles";
    File.WriteAllBytes(Path.Combine(outputPath, manName), manBytes);
    File.WriteAllText(Path.Combine(outputPath, manName + ".manifest"),
      "# Anity AssetBundleManifest\n" + string.Join("\n", manifest.GetAllAssetBundles()));

    return manifest;
  }

  /// <summary>Build all asset bundles from AssetDatabase labels (empty builds → no-op directory create).</summary>
  public static AssetBundleManifest BuildAssetBundles(string outputPath, BuildAssetBundleOptions assetBundleOptions, BuildTarget targetPlatform)
  {
    return BuildAssetBundles(outputPath, Array.Empty<AssetBundleBuild>(), assetBundleOptions, targetPlatform);
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
      return string.Empty;
    BuildAssetBundles(outputPath, Array.Empty<AssetBundleBuild>(), options, EditorUserBuildSettings.activeBuildTarget);
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
  ForceSingleInstance = 1 << 21,
  AcceptExternalModificationsToPlayer = 1 << 23,
  InstallInBuildFolder = 1 << 24,
  BuildScriptsOnly = 1 << 28
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
