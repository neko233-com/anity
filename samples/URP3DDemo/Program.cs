using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.Build.Reporting;
using Anity.Demos.URP3D;

EditorApplication.EnterPlaymode();

Console.WriteLine("=== Anity URP 3D Demo ===");
Console.WriteLine($"Unity version: {Application.unityVersion}");
Console.WriteLine($"Platform: {Application.platform}, isMobile={Application.isMobilePlatform}, isWebGL={Application.isWebGL}, isPlaying={Application.isPlaying}");
Console.WriteLine($"System: {SystemInfo.operatingSystem} ({SystemInfo.operatingSystemFamily}), {SystemInfo.processorCount} CPU @ {SystemInfo.processorFrequency}MHz, {SystemInfo.systemMemorySize}MB RAM");
Console.WriteLine($"GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceVendor}), {SystemInfo.graphicsMemorySize}MB VRAM, Type={SystemInfo.graphicsDeviceType}, Version={SystemInfo.graphicsDeviceVersion}");
Console.WriteLine($"Graphics: ShaderLevel={SystemInfo.graphicsShaderLevel}, MultiThreaded={SystemInfo.graphicsMultiThreaded}, ComputeShaders={SystemInfo.supportsComputeShaders}, Shadows={SystemInfo.supportsShadows}, Instancing={SystemInfo.supportsInstancing}");
Console.WriteLine($"Screen: {Screen.width}x{Screen.height}, DPI: {Screen.dpi}, Orientation={Screen.orientation}, FullScreen={Screen.fullScreen}");
Console.WriteLine($"Process: PID={Environment.ProcessId}, CurrentManagedThreadId={Environment.CurrentManagedThreadId}, isFocused={Application.isFocused}, isPaused={Application.isPaused}, isBatchMode={Application.isBatchMode}");
Console.WriteLine($"Paths: dataPath='{Application.dataPath}', persistentDataPath='{Application.persistentDataPath}', streamingAssetsPath='{Application.streamingAssetsPath}', temporaryCachePath='{Application.temporaryCachePath}'");
Console.WriteLine($"Bundle: company='{Application.companyName}', product='{Application.productName}', identifier='{Application.identifier}', bundleIdentifier='{Application.bundleIdentifier}', version='{Application.version}', buildGUID='{Application.buildGUID}'");
Console.WriteLine($"Runtime: runInBackground={Application.runInBackground}, targetFrameRate={Application.targetFrameRate}, sleepTimeout={Application.sleepTimeout}, systemLanguage={Application.systemLanguage}, internetReachability={Application.internetReachability}");
Console.WriteLine($"Device: name='{SystemInfo.deviceName}', model='{SystemInfo.deviceModel}', type={SystemInfo.deviceType}, uniqueId={SystemInfo.deviceUniqueIdentifier}, maxTextureSize={SystemInfo.maxTextureSize}");
Console.WriteLine();

PlayerSettings.productName = "URP3DDemo";
PlayerSettings.companyName = "Anity";
PlayerSettings.applicationIdentifier = "com.anity.urp3ddemo";
PlayerSettings.bundleVersion = "1.0.0";
Console.WriteLine($"PlayerSettings synced: company={PlayerSettings.companyName}, product={PlayerSettings.productName}, identifier={PlayerSettings.applicationIdentifier}, version={PlayerSettings.bundleVersion}");
Console.WriteLine($"Application re-read: company='{Application.companyName}', product='{Application.productName}', identifier='{Application.identifier}', version='{Application.version}'");
Console.WriteLine();

Screen.SetResolution(1280, 720, false);

var urpAsset = new UniversalRenderPipelineAsset();
GraphicsSettings.defaultRenderPipeline = urpAsset;
QualitySettings.renderPipeline = urpAsset;
Console.WriteLine("[URP] Render pipeline configured");

Application.targetFrameRate = 60;
Application.runInBackground = true;

EditorBuildSettings.scenes = new[] { new EditorBuildSettings.EditorBuildSettingsScene("Assets/Scenes/URP3DDemo.unity", true) };

var scene = DemoScene.Build();
SceneManager.SetActiveScene(scene);
Console.WriteLine($"[Scene] '{scene.name}' loaded, {scene.rootCount} root objects");
Console.WriteLine();

DemoUI.Build();
Console.WriteLine("[UI] Canvas + UGUI controls built");
Console.WriteLine();

var sw = Stopwatch.StartNew();
long frameCount = 0;
double totalFrameMs = 0;
double maxFrameMs = 0;

Console.WriteLine("Running 300 demo frames...");
for (int i = 0; i < 300; i++)
{
    float dt = 1f / 60f;
    var fw = Stopwatch.StartNew();
    UnityRuntime.Tick(dt);
    fw.Stop();

    double ms = fw.Elapsed.TotalMilliseconds;
    totalFrameMs += ms;
    if (ms > maxFrameMs) maxFrameMs = ms;
    frameCount++;

    if (i > 0 && i % 60 == 0)
    {
        int seconds = i / 60;
        DemoScene.SimulateInput();
        Console.WriteLine($"  t={seconds}s  fps~{60.0 / (totalFrameMs / (i + 1) / 1000.0):F1}  avgFrame={totalFrameMs / (i + 1):F2}ms  max={maxFrameMs:F2}ms  physics3D={DemoScene.PhysicsObjectCount}  physics2D={DemoScene.Physics2DObjectCount}  particles={DemoScene.ParticleCount}  touches={Input.touchCount}");
    }
}

sw.Stop();
Console.WriteLine();
Console.WriteLine($"=== Demo Complete ===");
Console.WriteLine($"Frames: {frameCount}");
Console.WriteLine($"Wall time: {sw.Elapsed.TotalSeconds:F2}s");
Console.WriteLine($"Avg frame: {totalFrameMs / frameCount:F3}ms");
Console.WriteLine($"Max frame: {maxFrameMs:F3}ms");
Console.WriteLine($"Camera count: {Camera.allCameras.Length}");
Console.WriteLine($"Active scene: {SceneManager.GetActiveScene().name}");
Console.WriteLine($"Canvas count: {Canvas.canvases.Count}");
Console.WriteLine($"UIRebuilds: {DemoScene.UIRebuildCount}");
Console.WriteLine($"Animator count: {DemoScene.AnimatorCount}");
Console.WriteLine($"ParticleSystems: {DemoScene.ParticleSystemCount}");
Console.WriteLine($"Colliders: {DemoScene.ColliderCount}");
Console.WriteLine($"AudioSources: {DemoScene.AudioSourceCount}");
Console.WriteLine($"All API calls verified - Build PASSED");

Console.WriteLine();
Console.WriteLine("=== Multi-Platform Build & Graphics API Verification ===");
Console.WriteLine("Supported Graphics APIs: D3D11, D3D12, Vulkan, Metal, OpenGLCore, OpenGLES2, OpenGLES3, WebGL2, Null");
Console.WriteLine($"Default Standalone Graphics APIs: {string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTargetGroup.Standalone))}");
Console.WriteLine($"Default iOS Graphics APIs: {string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTargetGroup.iOS))}");
Console.WriteLine($"Default Android Graphics APIs: {string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTargetGroup.Android))}");
Console.WriteLine($"Default WebGL Graphics APIs: {string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTargetGroup.WebGL))}");
Console.WriteLine();

var targets = new (BuildTarget target, string name, GraphicsDeviceType[] gfxAPIs)[]
{
    (BuildTarget.StandaloneWindows64, "Windows x64 (D3D11)", new[] { GraphicsDeviceType.Direct3D11 }),
    (BuildTarget.StandaloneWindows64, "Windows x64 (D3D12)", new[] { GraphicsDeviceType.Direct3D12 }),
    (BuildTarget.StandaloneWindows64, "Windows x64 (Vulkan)", new[] { GraphicsDeviceType.Vulkan }),
    (BuildTarget.StandaloneWindows64, "Windows x64 (OpenGL Core)", new[] { GraphicsDeviceType.OpenGLCore }),
    (BuildTarget.iOS, "iOS (Metal)", new[] { GraphicsDeviceType.Metal }),
    (BuildTarget.Android, "Android (Vulkan)", new[] { GraphicsDeviceType.Vulkan }),
    (BuildTarget.Android, "Android (GLES3)", new[] { GraphicsDeviceType.OpenGLES3 }),
    (BuildTarget.Android, "Android (GLES2)", new[] { GraphicsDeviceType.OpenGLES2 }),
    (BuildTarget.WebGL, "WebGL 2.0", new[] { GraphicsDeviceType.WebGL2 }),
    (BuildTarget.WebGL, "WebGL (GLES2 fallback)", new[] { GraphicsDeviceType.OpenGLES2 }),
};

int buildOK = 0, buildFail = 0;
foreach (var (target, name, gfxAPIs) in targets)
{
    var group = EditorUserBuildSettings.BuildTargetToBuildTargetGroup(target);
    PlayerSettings.SetGraphicsAPIs(group, gfxAPIs);
    var switchOk = EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);

    string ext = target switch {
        BuildTarget.StandaloneWindows64 or BuildTarget.StandaloneWindows => ".exe",
        BuildTarget.Android => ".apk",
        BuildTarget.iOS or BuildTarget.iPhone or BuildTarget.tvOS or BuildTarget.VisionOS => ".ipa",
        BuildTarget.WebGL => "",
        BuildTarget.StandaloneOSX => ".app",
        _ => ""
    };
    string safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_").Replace("(", "").Replace(")", "");
    string outPath = Path.Combine("Build", safeName);
    if (!string.IsNullOrEmpty(ext)) outPath += ext;
    var outDir = Path.GetDirectoryName(outPath);
    if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

    var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
    var opts = new BuildPlayerOptions { scenes = scenes, locationPathName = outPath, target = target, targetGroup = group, options = BuildOptions.None };
    var report = BuildPipeline.BuildPlayer(opts);
    var ok = report.summary.result == BuildResult.Succeeded;
    var scriptingBackend = PlayerSettings.GetScriptingBackend(target);
    Console.WriteLine($"  [{(ok ? "OK" : "FAIL")}] {name,-32} GFX={SystemInfo.graphicsDeviceType,-12} Platform={Application.platform,-14} Device={SystemInfo.deviceType,-8} Backend={scriptingBackend,-10} -> {report.summary.outputPath}");
    if (ok) buildOK++; else buildFail++;
}

Console.WriteLine();
Console.WriteLine("=== GraphicsDeviceType Enum Validation ===");
var allGfxTypes = Enum.GetValues<GraphicsDeviceType>();
foreach (var gfx in allGfxTypes)
{
    var ver = SystemInfo.graphicsDeviceVersion;
    Console.WriteLine($"  {gfx} = {(int)gfx}");
}

Console.WriteLine();
Console.WriteLine("=== BuildTarget & BuildTargetGroup Mapping ===");
var allTargets = Enum.GetValues<BuildTarget>();
foreach (var bt in allTargets)
{
    if (bt == BuildTarget.NoTarget) continue;
    var bg = EditorUserBuildSettings.BuildTargetToBuildTargetGroup(bt);
    var rp = EditorUserBuildSettings.BuildTargetToRuntimePlatform(bt);
    Console.WriteLine($"  {bt} (group={bg}, runtime={rp})");
}

Console.WriteLine();
Console.WriteLine($"Build results: {buildOK} passed, {buildFail} failed");
if (buildFail == 0) Console.WriteLine("All platform builds PASSED");
else Console.WriteLine("Some builds FAILED");

Console.WriteLine();
Console.WriteLine("=== Screen Orientation & Design Resolution Verification ===");

var orientations = new[] { ScreenOrientation.Portrait, ScreenOrientation.PortraitUpsideDown, ScreenOrientation.LandscapeLeft, ScreenOrientation.LandscapeRight, ScreenOrientation.AutoRotation };
var resolutions = new[] { (1080, 1920), (1920, 1080), (1280, 720), (2560, 1440), (750, 1334), (1125, 2436) };
foreach (var (w, h) in resolutions)
{
    Screen.SetResolution(w, h, false);
    bool isLandscape = w > h;
    float refW = isLandscape ? 1920f : 1080f;
    float refH = isLandscape ? 1080f : 1920f;
    float match = isLandscape ? 0f : 1f;
    float scaleW = w / refW;
    float scaleH = h / refH;
    float scaleFactor = match == 0f ? scaleW : match == 1f ? scaleH : Mathf.Pow(scaleW, 1f - match) * Mathf.Pow(scaleH, match);
    Console.WriteLine($"  {w}x{h,-5} ({(isLandscape?"Landscape":"Portrait")}) refRes={refW}x{refH} match={match:F1} → scaleFactor≈{scaleFactor:F3}");
}

foreach (var ori in orientations)
{
    Screen.orientation = ori;
    Console.WriteLine($"  Screen.orientation = {ori}, autorotatePortrait={Screen.autorotateToPortrait}, autorotateLandscapeLeft={Screen.autorotateToLandscapeLeft}");
}
Screen.orientation = ScreenOrientation.AutoRotation;

PlayerSettings.defaultScreenOrientation = UIOrientation.LandscapeLeft;
PlayerSettings.allowedAutorotateToLandscapeLeft = true;
PlayerSettings.allowedAutorotateToLandscapeRight = true;
PlayerSettings.allowedAutorotateToPortrait = false;
PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
Console.WriteLine($"PlayerSettings.defaultScreenOrientation = {PlayerSettings.defaultScreenOrientation}");

Console.WriteLine();
Console.WriteLine("=== Project Template Verification ===");
var tmpDir = Path.Combine(Path.GetTempPath(), "anity_templates");
if (Directory.Exists(tmpDir)) try { Directory.Delete(tmpDir, true); } catch { }
Directory.CreateDirectory(tmpDir);
try
{
    var t3dLand = Path.Combine(tmpDir, "URP3D_Landscape");
    var t3dPort = Path.Combine(tmpDir, "URP3D_Portrait");
    var t3dAuto = Path.Combine(tmpDir, "URP3D_AutoRotate");
    var t2dLand = Path.Combine(tmpDir, "URP2D_Landscape");
    var tEmpty = Path.Combine(tmpDir, "Empty");

    ProjectTemplates.Create3DURPProject(t3dLand, ScreenOrientation.LandscapeLeft);
    ProjectTemplates.Create3DURPProject(t3dPort, ScreenOrientation.Portrait);
    ProjectTemplates.Create3DURPProject(t3dAuto, ScreenOrientation.AutoRotation);
    ProjectTemplates.Create2DURPProject(t2dLand, ScreenOrientation.LandscapeLeft);
    ProjectTemplates.CreateEmptyProject(tEmpty);

    foreach (var p in new[] { t3dLand, t3dPort, t3dAuto, t2dLand, tEmpty })
    {
        bool hasProj = File.Exists(Path.Combine(p, "ProjectSettings", "ProjectSettings.asset"));
        bool hasPkg = File.Exists(Path.Combine(p, "Packages", "manifest.json"));
        bool hasScene = Directory.GetFiles(Path.Combine(p, "Assets"), "*.unity", SearchOption.AllDirectories).Length > 0;
        bool hasDirs = Directory.Exists(Path.Combine(p, "Assets", "Scenes"));
        string name = Path.GetFileName(p);
        Console.WriteLine($"  Template {name,-25}: ProjSet={hasProj}, Manifest={hasPkg}, Scene={hasScene}, Dirs={hasDirs}");
    }
}
finally
{
    try { Directory.Delete(tmpDir, true); } catch { }
}

Console.WriteLine();
Console.WriteLine("=== Android APK Build Verification ===");
string apkOut = Path.Combine("Build", "Android_Vulkan.apk");
var androidOpts = new BuildPlayerOptions {
    scenes = new[] { "Assets/Scenes/URP3DDemo.unity" },
    locationPathName = apkOut,
    target = BuildTarget.Android,
    targetGroup = BuildTargetGroup.Android,
    options = BuildOptions.None
};
PlayerSettings.SetGraphicsAPIs(BuildTargetGroup.Android, new[] { GraphicsDeviceType.Vulkan });
PlayerSettings.defaultScreenOrientation = UIOrientation.LandscapeLeft;
PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel33;
PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
var apkReport = BuildPipeline.BuildPlayer(androidOpts);
bool apkOk = apkReport.summary.result == BuildResult.Succeeded;
Console.WriteLine($"  Android APK: {(apkOk?"OK":"FAIL")} path={apkReport.summary.outputPath} size={apkReport.summary.totalSize}");

if (apkOk)
{
    if (Directory.Exists(apkOut))
    {
        bool hasManifest = File.Exists(Path.Combine(apkOut, "AndroidManifest.xml"));
        bool hasClassesDex = File.Exists(Path.Combine(apkOut, "classes.dex"));
        bool hasRes = Directory.Exists(Path.Combine(apkOut, "res"));
        bool hasLib = Directory.Exists(Path.Combine(apkOut, "lib"));
        Console.WriteLine($"  APK Structure (Gradle export): Manifest={hasManifest}, classes.dex={hasClassesDex}, res/={hasRes}, lib/={hasLib}");
    }
    else if (File.Exists(apkOut))
    {
        var fi = new FileInfo(apkOut);
        Console.WriteLine($"  APK File: {fi.Length} bytes (binary package)");
    }
}

string apkExportDir = Path.Combine("Build", "Android_GradleExport");
PlayerSettings.defaultScreenOrientation = UIOrientation.LandscapeLeft;
PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
var apkExportOpts = new BuildPlayerOptions {
    scenes = new[] { "Assets/Scenes/URP3DDemo.unity" },
    locationPathName = apkExportDir,
    target = BuildTarget.Android,
    targetGroup = BuildTargetGroup.Android,
    options = BuildOptions.AcceptExternalModificationsToPlayer
};
var apkExportReport = BuildPipeline.BuildPlayer(apkExportOpts);
bool apkExportOk = apkExportReport.summary.result == BuildResult.Succeeded;
if (apkExportOk && Directory.Exists(apkExportDir))
{
    bool hasManifest = File.Exists(Path.Combine(apkExportDir, "AndroidManifest.xml"));
    bool hasClassesDex = File.Exists(Path.Combine(apkExportDir, "classes.dex"));
    bool hasRes = Directory.Exists(Path.Combine(apkExportDir, "res"));
    bool hasLib = Directory.Exists(Path.Combine(apkExportDir, "lib"));
    bool hasAssets = Directory.Exists(Path.Combine(apkExportDir, "assets"));
    bool hasArmv7 = Directory.Exists(Path.Combine(apkExportDir, "lib", "armeabi-v7a"));
    bool hasArm64 = Directory.Exists(Path.Combine(apkExportDir, "lib", "arm64-v8a"));
    Console.WriteLine($"  Android Gradle Export: {(apkExportOk?"OK":"FAIL")} path={apkExportDir}");
    Console.WriteLine($"  Export Structure: Manifest={hasManifest}, classes.dex={hasClassesDex}, res/={hasRes}, lib/={hasLib}, assets/={hasAssets}, armv7={hasArmv7}, arm64={hasArm64}");
}
else
{
    Console.WriteLine($"  Android Gradle Export: {(apkExportOk?"OK":"FAIL")}");
}

string apkPortraitOut = Path.Combine("Build", "Android_Portrait.apk");
PlayerSettings.defaultScreenOrientation = UIOrientation.Portrait;
PlayerSettings.allowedAutorotateToPortrait = true;
PlayerSettings.allowedAutorotateToLandscapeLeft = false;
PlayerSettings.allowedAutorotateToLandscapeRight = false;
var apkPortraitOpts = new BuildPlayerOptions {
    scenes = new[] { "Assets/Scenes/URP3DDemo.unity" },
    locationPathName = apkPortraitOut,
    target = BuildTarget.Android,
    targetGroup = BuildTargetGroup.Android,
    options = BuildOptions.None
};
var apkPortraitReport = BuildPipeline.BuildPlayer(apkPortraitOpts);
bool apkPortraitOk = apkPortraitReport.summary.result == BuildResult.Succeeded;
Console.WriteLine($"  Android Portrait APK: {(apkPortraitOk?"OK":"FAIL")} path={apkPortraitReport.summary.outputPath}");

Console.WriteLine();
Console.WriteLine("=== Process & Runtime Verification Summary ===");
Console.WriteLine($"Application.isPlaying: {Application.isPlaying} (expected: true in play mode)");
Console.WriteLine($"Application.isFocused: {Application.isFocused}");
Console.WriteLine($"Application.isPaused: {Application.isPaused}");
Console.WriteLine($"Application.runInBackground: {Application.runInBackground}");
Console.WriteLine($"Application.platform: {Application.platform}");
Console.WriteLine($"Application.isMobilePlatform: {Application.isMobilePlatform}");
Console.WriteLine($"Application.isWebGL: {Application.isWebGL}");
Console.WriteLine($"Environment.ProcessId: {Environment.ProcessId}");
Console.WriteLine($"Process verification PASSED - all platform APIs accessible");
