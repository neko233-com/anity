using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEditor;

public static class ProjectSettings
{
    private static readonly Dictionary<string, object> _settings = new();

    public static string productName
    {
        get => GetValue<string>(nameof(productName));
        set => SetValue(nameof(productName), value);
    }

    public static string companyName
    {
        get => GetValue<string>(nameof(companyName));
        set => SetValue(nameof(companyName), value);
    }

    public static string applicationIdentifier
    {
        get => GetValue<string>(nameof(applicationIdentifier));
        set => SetValue(nameof(applicationIdentifier), value);
    }

    public static string bundleVersion
    {
        get => GetValue<string>(nameof(bundleVersion));
        set => SetValue(nameof(bundleVersion), value);
    }

    public static string cloudProjectId
    {
        get => GetValue<string>(nameof(cloudProjectId));
        set => SetValue(nameof(cloudProjectId), value);
    }

    public static UIOrientation defaultScreenOrientation
    {
        get => GetValue<UIOrientation>(nameof(defaultScreenOrientation), UIOrientation.AutoRotation);
        set => SetValue(nameof(defaultScreenOrientation), value);
    }

    public static bool defaultIsFullScreen
    {
        get => GetValue<bool>(nameof(defaultIsFullScreen), true);
        set => SetValue(nameof(defaultIsFullScreen), value);
    }

    public static int defaultScreenWidth
    {
        get => GetValue<int>(nameof(defaultScreenWidth), 1920);
        set => SetValue(nameof(defaultScreenWidth), value);
    }

    public static int defaultScreenHeight
    {
        get => GetValue<int>(nameof(defaultScreenHeight), 1080);
        set => SetValue(nameof(defaultScreenHeight), value);
    }

    public static bool usePlayerLog
    {
        get => GetValue<bool>(nameof(usePlayerLog), true);
        set => SetValue(nameof(usePlayerLog), value);
    }

    public static bool resizableWindow
    {
        get => GetValue<bool>(nameof(resizableWindow));
        set => SetValue(nameof(resizableWindow), value);
    }

    public static bool useMacAppStoreValidation
    {
        get => GetValue<bool>(nameof(useMacAppStoreValidation));
        set => SetValue(nameof(useMacAppStoreValidation), value);
    }

    public static bool runInBackground
    {
        get => GetValue<bool>(nameof(runInBackground), true);
        set => SetValue(nameof(runInBackground), value);
    }

    public static bool captureSingleScreen
    {
        get => GetValue<bool>(nameof(captureSingleScreen));
        set => SetValue(nameof(captureSingleScreen), value);
    }

    public static bool muteOtherAudioSources
    {
        get => GetValue<bool>(nameof(muteOtherAudioSources));
        set => SetValue(nameof(muteOtherAudioSources), value);
    }

    public static bool PrepareIOSForRecording
    {
        get => GetValue<bool>(nameof(PrepareIOSForRecording));
        set => SetValue(nameof(PrepareIOSForRecording), value);
    }

    public static iOSShowActivityIndicatorOnLoading iosShowActivityIndicatorOnLoading
    {
        get => GetValue<iOSShowActivityIndicatorOnLoading>(nameof(iosShowActivityIndicatorOnLoading));
        set => SetValue(nameof(iosShowActivityIndicatorOnLoading), value);
    }

    public static iOSAppInBackgroundBehavior iosAppInBackgroundBehavior
    {
        get => GetValue<iOSAppInBackgroundBehavior>(nameof(iosAppInBackgroundBehavior));
        set => SetValue(nameof(iosAppInBackgroundBehavior), value);
    }

    public static bool iosAllowHTTPDownload
    {
        get => GetValue<bool>(nameof(iosAllowHTTPDownload), true);
        set => SetValue(nameof(iosAllowHTTPDownload), value);
    }

    public static int androidBundleVersionCode
    {
        get => GetValue<int>(nameof(androidBundleVersionCode), 1);
        set => SetValue(nameof(androidBundleVersionCode), value);
    }

    public static AndroidSdkVersions androidMinSdkVersion
    {
        get => GetValue<AndroidSdkVersions>(nameof(androidMinSdkVersion));
        set => SetValue(nameof(androidMinSdkVersion), value);
    }

    public static AndroidSdkVersions androidTargetSdkVersion
    {
        get => GetValue<AndroidSdkVersions>(nameof(androidTargetSdkVersion));
        set => SetValue(nameof(androidTargetSdkVersion), value);
    }

    public static ScriptingImplementation scriptingBackend
    {
        get => GetValue<ScriptingImplementation>(nameof(scriptingBackend));
        set => SetValue(nameof(scriptingBackend), value);
    }

    public static ApiCompatibilityLevel apiCompatibilityLevel
    {
        get => GetValue<ApiCompatibilityLevel>(nameof(apiCompatibilityLevel));
        set => SetValue(nameof(apiCompatibilityLevel), value);
    }

    public static StrippingLevel strippingLevel
    {
        get => GetValue<StrippingLevel>(nameof(strippingLevel));
        set => SetValue(nameof(strippingLevel), value);
    }

    public static int vSyncCount
    {
        get => GetValue<int>(nameof(vSyncCount), 1);
        set => SetValue(nameof(vSyncCount), value);
    }

    public static int antiAliasing
    {
        get => GetValue<int>(nameof(antiAliasing));
        set => SetValue(nameof(antiAliasing), value);
    }

    public static int targetFrameRate
    {
        get => GetValue<int>(nameof(targetFrameRate), -1);
        set => SetValue(nameof(targetFrameRate), value);
    }

    public static AspectRatio[] supportedAspectRatios
    {
        get => GetValue<AspectRatio[]>(nameof(supportedAspectRatios), Array.Empty<AspectRatio>());
        set => SetValue(nameof(supportedAspectRatios), value);
    }

    public static AspectRatio aspectRatio
    {
        get => GetValue<AspectRatio>(nameof(aspectRatio));
        set => SetValue(nameof(aspectRatio), value);
    }

    public static string locationUsageDescription
    {
        get => GetValue<string>(nameof(locationUsageDescription));
        set => SetValue(nameof(locationUsageDescription), value);
    }

    public static string cameraUsageDescription
    {
        get => GetValue<string>(nameof(cameraUsageDescription));
        set => SetValue(nameof(cameraUsageDescription), value);
    }

    public static string microphoneUsageDescription
    {
        get => GetValue<string>(nameof(microphoneUsageDescription));
        set => SetValue(nameof(microphoneUsageDescription), value);
    }

    public static bool graphicsJobs
    {
        get => GetValue<bool>(nameof(graphicsJobs));
        set => SetValue(nameof(graphicsJobs), value);
    }

    public static bool multithreadedRendering
    {
        get => GetValue<bool>(nameof(multithreadedRendering), true);
        set => SetValue(nameof(multithreadedRendering), value);
    }

    public static GraphicsDeviceType[] graphicsAPIs
    {
        get => GetValue<GraphicsDeviceType[]>(nameof(graphicsAPIs), Array.Empty<GraphicsDeviceType>());
        set => SetValue(nameof(graphicsAPIs), value);
    }

    public static ColorSpace colorSpace
    {
        get => GetValue<ColorSpace>(nameof(colorSpace), ColorSpace.Gamma);
        set => SetValue(nameof(colorSpace), value);
    }

    public static bool mTRendering
    {
        get => GetValue<bool>(nameof(mTRendering));
        set => SetValue(nameof(mTRendering), value);
    }

    public static bool ProtectGraphicsMemory
    {
        get => GetValue<bool>(nameof(ProtectGraphicsMemory));
        set => SetValue(nameof(ProtectGraphicsMemory), value);
    }

    public static bool FramebufferDepthMemory
    {
        get => GetValue<bool>(nameof(FramebufferDepthMemory));
        set => SetValue(nameof(FramebufferDepthMemory), value);
    }

    public static bool bakeCollisionMeshes
    {
        get => EditorSettings.bakeCollisionMeshes;
        set => EditorSettings.bakeCollisionMeshes = value;
    }

    public static T GetSingleton<T>() where T : class, new() => new T();
    public static void SetSingleton<T>(T singleton) where T : class { _ = singleton; }

    public static string projectPath => string.Empty;
    public static string[] availableProjectPaths => Array.Empty<string>();

    public static bool InitializeOnStartup(string projectPath) { _ = projectPath; return true; }

    public static bool TryGetSetting<T>(string key, out T value)
    {
        if (_settings.TryGetValue(key, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }
        value = default!;
        return false;
    }

    public static void SetSetting<T>(string key, T value)
    {
        _settings[key] = value!;
    }

    public static void DeleteSetting(string key)
    {
        _settings.Remove(key);
    }

    private static T GetValue<T>(string key, T defaultValue = default!)
    {
        if (_settings.TryGetValue(key, out var value) && value is T typedValue)
            return typedValue;
        return defaultValue;
    }

    private static void SetValue<T>(string key, T value)
    {
        _settings[key] = value!;
    }
}

public enum iOSShowActivityIndicatorOnLoading
{
    WhiteLarge,
    White,
    Gray,
    DontShow
}

public enum iOSAppInBackgroundBehavior
{
    Suspend,
    Exit,
    Custom
}

public enum StrippingLevel
{
    Disabled,
    Low,
    Medium,
    High
}

public enum AspectRatio
{
    Aspect4By3,
    Aspect5By4,
    Aspect16By9,
    Aspect16By10,
    AspectOthers
}

public enum AndroidSdkVersions
{
    AndroidApiLevel16 = 16,
    AndroidApiLevel17 = 17,
    AndroidApiLevel18 = 18,
    AndroidApiLevel19 = 19,
    AndroidApiLevel21 = 21,
    AndroidApiLevel22 = 22,
    AndroidApiLevel23 = 23,
    AndroidApiLevel24 = 24,
    AndroidApiLevel25 = 25,
    AndroidApiLevel26 = 26,
    AndroidApiLevel27 = 27,
    AndroidApiLevel28 = 28,
    AndroidApiLevel29 = 29,
    AndroidApiLevel30 = 30,
    AndroidApiLevel31 = 31,
    AndroidApiLevel32 = 32,
    AndroidApiLevel33 = 33,
    AndroidApiLevel34 = 34,
    AndroidApiLevelAuto = 0
}
