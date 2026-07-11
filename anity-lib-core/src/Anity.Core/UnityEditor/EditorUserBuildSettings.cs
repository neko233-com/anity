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
        BuildTarget.StandaloneWindows64 or BuildTarget.StandaloneWindows or BuildTarget.StandaloneLinux64 or BuildTarget.StandaloneOSX => BuildTargetGroup.Standalone,
        BuildTarget.iOS => BuildTargetGroup.iOS,
        BuildTarget.Android => BuildTargetGroup.Android,
        BuildTarget.WebGL => BuildTargetGroup.WebGL,
        _ => BuildTargetGroup.Unknown
    };

    public static RuntimePlatform BuildTargetToRuntimePlatform(BuildTarget target) => target switch
    {
        BuildTarget.StandaloneWindows64 or BuildTarget.StandaloneWindows => RuntimePlatform.WindowsPlayer,
        BuildTarget.StandaloneOSX => RuntimePlatform.OSXPlayer,
        BuildTarget.StandaloneLinux64 => RuntimePlatform.LinuxPlayer,
        BuildTarget.iOS => RuntimePlatform.IPhonePlayer,
        BuildTarget.Android => RuntimePlatform.Android,
        BuildTarget.WebGL => RuntimePlatform.WebGLPlayer,
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
