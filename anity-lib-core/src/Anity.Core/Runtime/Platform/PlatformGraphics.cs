using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Anity.Core.Runtime.Platform;

/// <summary>
/// Platform graphics defaults:
/// - iOS / tvOS / visionOS → Metal only
/// - Android → Vulkan primary (GLES3/2 fallback)
/// - WebGL → WebGL2
/// - Desktop → D3D11/12 / Metal / Vulkan / GL by OS
/// </summary>
public enum PlatformGraphicsBackend
{
    Auto,
    Metal,
    Vulkan,
    Direct3D11,
    Direct3D12,
    OpenGLES3,
    OpenGLES2,
    OpenGLCore,
    WebGL2,
    Null
}

public static class PlatformGraphics
{
    private static GraphicsDeviceType? _forcedDeviceType;
    private static TargetPlatform? _forcedPlatform;

    public static GraphicsDeviceType ActiveDeviceType
    {
        get
        {
            if (_forcedDeviceType.HasValue)
                return _forcedDeviceType.Value;
            return GetDefaultDeviceType(PlatformConfig.CurrentPlatform);
        }
    }

    public static TargetPlatform ActivePlatform =>
        _forcedPlatform ?? PlatformConfig.CurrentPlatform;

    public static bool IsMetal => ActiveDeviceType == GraphicsDeviceType.Metal;
    public static bool IsVulkan => ActiveDeviceType == GraphicsDeviceType.Vulkan;
    public static bool IsMobileMetalOrVulkan =>
        (ActivePlatform == TargetPlatform.iOS && IsMetal) ||
        (ActivePlatform == TargetPlatform.Android && IsVulkan);

    public static void ForceGraphicsDevice(GraphicsDeviceType type)
    {
        _forcedDeviceType = type;
        SystemInfo.overrideGraphicsDeviceType = type;
        if (type == GraphicsDeviceType.Metal)
            SystemInfo._overrideDeviceType = DeviceType.Handheld;
        else if (type == GraphicsDeviceType.Vulkan && ActivePlatform == TargetPlatform.Android)
            SystemInfo._overrideDeviceType = DeviceType.Handheld;
    }

    public static void ForcePlatform(TargetPlatform platform)
    {
        _forcedPlatform = platform;
        PlatformConfig.SetTargetPlatform(platform);
        ForceGraphicsDevice(GetDefaultDeviceType(platform));
        ApplyPlatformDeviceProfile(platform);
    }

    public static void ClearForce()
    {
        _forcedDeviceType = null;
        _forcedPlatform = null;
        SystemInfo.overrideGraphicsDeviceType = null;
        SystemInfo._overrideDeviceType = null;
    }

    public static GraphicsDeviceType GetDefaultDeviceType(TargetPlatform platform) => platform switch
    {
        TargetPlatform.iOS => GraphicsDeviceType.Metal,
        TargetPlatform.Android => GraphicsDeviceType.Vulkan,
        TargetPlatform.WebGL => GraphicsDeviceType.WebGL2,
        TargetPlatform.MacOS => GraphicsDeviceType.Metal,
        TargetPlatform.Windows => GraphicsDeviceType.Direct3D11,
        TargetPlatform.Linux => GraphicsDeviceType.Vulkan,
        _ => GraphicsDeviceType.Direct3D11
    };

    public static GraphicsDeviceType[] GetPreferredApis(TargetPlatform platform) => platform switch
    {
        TargetPlatform.iOS => new[] { GraphicsDeviceType.Metal },
        TargetPlatform.Android => new[]
        {
            GraphicsDeviceType.Vulkan,
            GraphicsDeviceType.OpenGLES3,
            GraphicsDeviceType.OpenGLES2
        },
        TargetPlatform.WebGL => new[] { GraphicsDeviceType.WebGL2, GraphicsDeviceType.OpenGLES2 },
        TargetPlatform.MacOS => new[] { GraphicsDeviceType.Metal, GraphicsDeviceType.OpenGLCore },
        TargetPlatform.Windows => new[]
        {
            GraphicsDeviceType.Direct3D11,
            GraphicsDeviceType.Direct3D12,
            GraphicsDeviceType.Vulkan,
            GraphicsDeviceType.OpenGLCore
        },
        TargetPlatform.Linux => new[]
        {
            GraphicsDeviceType.Vulkan,
            GraphicsDeviceType.OpenGLCore
        },
        _ => new[] { GraphicsDeviceType.Direct3D11 }
    };

    public static TextureFormat GetPreferredTextureFormat(bool hasAlpha, bool highQuality = true) =>
        TextureCompressionUtility.GetDefaultFormatForGraphicsAPI(ActiveDeviceType, hasAlpha, highQuality);

    public static bool SupportsTextureFormat(TextureFormat format) =>
        TextureCompressionUtility.IsFormatSupportedOnAPI(format, ActiveDeviceType);

    public static string GetShaderTargetName() => ActiveDeviceType switch
    {
        GraphicsDeviceType.Metal => "metal",
        GraphicsDeviceType.Vulkan => "vulkan",
        GraphicsDeviceType.Direct3D11 => "d3d11",
        GraphicsDeviceType.Direct3D12 => "d3d12",
        GraphicsDeviceType.OpenGLES3 => "gles3",
        GraphicsDeviceType.OpenGLES2 => "gles",
        GraphicsDeviceType.OpenGLCore => "glcore",
        GraphicsDeviceType.WebGL2 => "webgl",
        _ => "unknown"
    };

    public static int GetDefaultMSAA() => ActiveDeviceType switch
    {
        GraphicsDeviceType.Metal => 4,
        GraphicsDeviceType.Vulkan => 4,
        GraphicsDeviceType.WebGL2 => 2,
        _ => 4
    };

    public static bool SupportsComputeShaders => ActiveDeviceType switch
    {
        GraphicsDeviceType.OpenGLES2 or GraphicsDeviceType.Null => false,
        GraphicsDeviceType.WebGL2 => false,
        _ => true
    };

    public static bool SupportsGeometryShaders => ActiveDeviceType switch
    {
        GraphicsDeviceType.Metal => false, // Metal has no geometry shaders
        GraphicsDeviceType.OpenGLES2 or GraphicsDeviceType.OpenGLES3 or GraphicsDeviceType.WebGL2 => false,
        _ => true
    };

    public static bool SupportsTessellation => ActiveDeviceType switch
    {
        GraphicsDeviceType.Metal or GraphicsDeviceType.Vulkan
            or GraphicsDeviceType.Direct3D11 or GraphicsDeviceType.Direct3D12
            or GraphicsDeviceType.OpenGLCore => true,
        _ => false
    };

    /// <summary>
    /// Configure runtime as iOS + Metal (player simulation / CI matrix).
    /// </summary>
    public static void ConfigureIOSMetal()
    {
        ForcePlatform(TargetPlatform.iOS);
        ForceGraphicsDevice(GraphicsDeviceType.Metal);
        QualitySettings.antiAliasing = 4;
    }

    /// <summary>
    /// Configure runtime as Android + Vulkan primary.
    /// </summary>
    public static void ConfigureAndroidVulkan()
    {
        ForcePlatform(TargetPlatform.Android);
        ForceGraphicsDevice(GraphicsDeviceType.Vulkan);
        QualitySettings.antiAliasing = 4;
    }

    private static void ApplyPlatformDeviceProfile(TargetPlatform platform)
    {
        switch (platform)
        {
            case TargetPlatform.iOS:
                SystemInfo._overrideDeviceType = DeviceType.Handheld;
                break;
            case TargetPlatform.Android:
                SystemInfo._overrideDeviceType = DeviceType.Handheld;
                break;
            case TargetPlatform.WebGL:
                SystemInfo._overrideDeviceType = DeviceType.Desktop;
                break;
            default:
                SystemInfo._overrideDeviceType = DeviceType.Desktop;
                break;
        }
    }
}
