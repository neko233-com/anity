using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEditor;

public static class EditorUserBuildSettings
{
    private static BuildTarget _activeBuildTarget = BuildTarget.StandaloneWindows64;
    private static BuildTargetGroup _activeBuildTargetGroup = BuildTargetGroup.Standalone;
    public static event Action<BuildTarget>? activeBuildTargetChanged;

    public static BuildTarget activeBuildTarget
    {
        get => _activeBuildTarget;
        set => SwitchActiveBuildTarget(value);
    }

    public static BuildTargetGroup selectedBuildTargetGroup
    {
        get => _activeBuildTargetGroup;
        set => _activeBuildTargetGroup = value;
    }

    public static bool SwitchActiveBuildTarget(BuildTarget target)
    {
        var group = BuildTargetToBuildTargetGroup(target);
        _activeBuildTarget = target;
        _activeBuildTargetGroup = group;
        ApplyBuildTargetOverrides(target, group);
        activeBuildTargetChanged?.Invoke(target);
        return true;
    }

    public static bool SwitchActiveBuildTarget(BuildTargetGroup group, BuildTarget target)
    {
        _activeBuildTargetGroup = group;
        return SwitchActiveBuildTarget(target);
    }

    public static BuildTargetGroup BuildTargetToBuildTargetGroup(BuildTarget target) => target switch
    {
        BuildTarget.StandaloneWindows64 or BuildTarget.StandaloneWindows or BuildTarget.StandaloneLinux64 or BuildTarget.StandaloneLinux or BuildTarget.StandaloneLinuxUniversal or BuildTarget.StandaloneOSX or BuildTarget.StandaloneOSXIntel or BuildTarget.StandaloneOSXUniversal => BuildTargetGroup.Standalone,
        BuildTarget.iOS or BuildTarget.iPhone => BuildTargetGroup.iOS,
        BuildTarget.tvOS => BuildTargetGroup.tvOS,
        BuildTarget.VisionOS => BuildTargetGroup.VisionOS,
        BuildTarget.Android => BuildTargetGroup.Android,
        BuildTarget.WebGL => BuildTargetGroup.WebGL,
        BuildTarget.WSAPlayer => BuildTargetGroup.WSA,
        BuildTarget.PS4 => BuildTargetGroup.PS4,
        BuildTarget.PS5 => BuildTargetGroup.PS5,
        BuildTarget.XboxOne or BuildTarget.XboxOneD3D12 or BuildTarget.GameCoreXboxOne => BuildTargetGroup.XboxOne,
        BuildTarget.GameCoreXboxSeries => BuildTargetGroup.GameCoreXboxSeries,
        BuildTarget.Switch => BuildTargetGroup.Switch,
        BuildTarget.Lumin => BuildTargetGroup.Lumin,
        BuildTarget.Stadia => BuildTargetGroup.Stadia,
        BuildTarget.EmbeddedLinux or BuildTarget.LinuxHeadlessSimulation => BuildTargetGroup.EmbeddedLinux,
        _ => BuildTargetGroup.Unknown
    };

    public static RuntimePlatform BuildTargetToRuntimePlatform(BuildTarget target) => target switch
    {
        BuildTarget.StandaloneWindows64 or BuildTarget.StandaloneWindows => RuntimePlatform.WindowsPlayer,
        BuildTarget.StandaloneOSX or BuildTarget.StandaloneOSXIntel or BuildTarget.StandaloneOSXUniversal => RuntimePlatform.OSXPlayer,
        BuildTarget.StandaloneLinux64 or BuildTarget.StandaloneLinux or BuildTarget.StandaloneLinuxUniversal => RuntimePlatform.LinuxPlayer,
        BuildTarget.iOS or BuildTarget.iPhone or BuildTarget.tvOS or BuildTarget.VisionOS => RuntimePlatform.IPhonePlayer,
        BuildTarget.Android => RuntimePlatform.Android,
        BuildTarget.WebGL => RuntimePlatform.WebGLPlayer,
        BuildTarget.WSAPlayer => RuntimePlatform.WindowsPlayer,
        BuildTarget.Switch => RuntimePlatform.Switch,
        BuildTarget.PS4 or BuildTarget.PS5 => RuntimePlatform.PS4,
        BuildTarget.XboxOne or BuildTarget.XboxOneD3D12 or BuildTarget.GameCoreXboxOne or BuildTarget.GameCoreXboxSeries => RuntimePlatform.XboxOne,
        _ => RuntimePlatform.WindowsPlayer
    };

    private static void ApplyBuildTargetOverrides(BuildTarget target, BuildTargetGroup group)
    {
        var platform = BuildTargetToRuntimePlatform(target);
        var appType = typeof(Application);
        var ovPlat = appType.GetField("_overridePlatform", BindingFlags.NonPublic | BindingFlags.Static);
        ovPlat?.SetValue(null, platform);

        DeviceType dt = target switch { BuildTarget.iOS or BuildTarget.Android => DeviceType.Handheld, _ => DeviceType.Desktop };
        var siType = typeof(SystemInfo);
        var ovDevType = siType.GetField("_overrideDeviceType", BindingFlags.NonPublic | BindingFlags.Static);
        if (ovDevType != null) ovDevType.SetValue(null, dt);

        var apis = PlayerSettings.GetGraphicsAPIs(group);
        if (apis != null && apis.Length > 0)
        {
            var ovGfx = siType.GetProperty("overrideGraphicsDeviceType");
            ovGfx?.SetValue(null, apis[0]);
        }
    }

    public static string buildLocation { get; set; } = "Build";
    public static bool developmentBuild { get; set; } = false;
    public static bool connectProfiler { get; set; } = false;
    public static bool allowDebugging { get; set; } = false;
    public static bool buildScriptsOnly { get; set; } = false;
    public static MobileTextureSubtarget androidBuildSubtarget { get; set; } = MobileTextureSubtarget.Generic;
    public static bool compressBuild { get; set; } = true;
}

public enum MobileTextureSubtarget
{
    Generic,
    DXT,
    PVRTC,
    ATC,
    ETC,
    ETC2,
    ASTC
}

[Flags]
public enum AndroidArchitecture
{
    None = 0,
    ARMv7 = 1,
    ARM64 = 2,
    X86 = 4,
    X86_64 = 8,
    All = ARMv7 | ARM64 | X86 | X86_64
}

public enum AndroidGamepadSupportLevel
{
    Disabled = 0,
    SupportsDPad = 1,
    SupportsGamepad = 2
}

public enum WebGLCompressionFormat
{
    Brotli,
    Gzip,
    Disabled
}

public enum WebGLLinkerTarget
{
    Asm,
    Wasm,
    Both
}

public enum WebGLExceptionSupport
{
    None,
    ExplicitlyThrownExceptionsOnly,
    FullWithStacktrace
}

[Flags]
public enum UIInterfaceOrientationMask
{
    Portrait = 1,
    PortraitUpsideDown = 2,
    LandscapeLeft = 4,
    LandscapeRight = 8,
    AllButUpsideDown = Portrait | LandscapeLeft | LandscapeRight,
    All = AllButUpsideDown | PortraitUpsideDown
}
