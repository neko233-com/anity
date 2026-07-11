using System.Runtime.InteropServices;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine;

[Flags]
public enum CopyTextureSupport
{
    None = 0,
    Basic = (1 << 0),
    Copy3D = (1 << 1),
    DifferentTypes = (1 << 2),
    TextureToRT = (1 << 3),
    RTToTexture = (1 << 4)
}

public static class SystemInfo
{
    private static string _deviceUniqueIdentifier;
    internal static DeviceType? _overrideDeviceType;

    public static string deviceUniqueIdentifier
    {
        get
        {
            if (_deviceUniqueIdentifier == null)
            {
                _deviceUniqueIdentifier = Guid.NewGuid().ToString("N");
            }
            return _deviceUniqueIdentifier;
        }
    }

    public static string deviceName => Environment.MachineName;
    public static string deviceModel => "Anity Device";
    public static DeviceType deviceType => _overrideDeviceType ?? DeviceType.Desktop;
    public static string operatingSystem => RuntimeInformation.OSDescription;
    public static OperatingSystemFamily operatingSystemFamily => GetOSFamily();
    public static string processorType => RuntimeInformation.ProcessArchitecture.ToString();
    public static int processorCount => Environment.ProcessorCount;
    public static int processorFrequency => 3000;
    public static int systemMemorySize => 16384;
    public static string graphicsDeviceName => "Anity GPU";
    public static string graphicsDeviceVendor => "Anity";
    public static int graphicsDeviceID => 0;
    public static int graphicsDeviceVendorID => 0;
    public static string graphicsDeviceVersion => GetGraphicsDeviceVersion(graphicsDeviceType);
    public static int graphicsMemorySize => 4096;
    public static GraphicsDeviceType? overrideGraphicsDeviceType { get; set; }
    public static GraphicsDeviceType graphicsDeviceType => overrideGraphicsDeviceType ?? GetGraphicsDeviceType();
    public static bool IsWebGL => graphicsDeviceType == GraphicsDeviceType.WebGL2 || graphicsDeviceType == GraphicsDeviceType.OpenGLES2;
    public static bool IsMobile => deviceType == DeviceType.Handheld;
    public static int graphicsShaderLevel => 50;
    public static bool graphicsMultiThreaded => false;
    public static CopyTextureSupport copyTextureSupport => CopyTextureSupport.Basic | CopyTextureSupport.Copy3D | CopyTextureSupport.DifferentTypes | CopyTextureSupport.TextureToRT | CopyTextureSupport.RTToTexture;
    public static bool supportsShadows => true;
    public static bool supportsRawShadowDepthSampling => true;
    public static bool supportsMotionVectors => true;
    public static bool supportsComputeShadows => true;
    public static bool supports3DTextures => true;
    public static bool supports2DArrayTextures => true;
    public static bool supportsCubemapArrayTextures => true;
    public static bool supportsComputeShaders => true;
    public static bool supportsInstancing => true;
    public static bool supportsGeometryShaders => true;
    public static bool supportsTessellationShaders => true;
    public static bool supportsHardwareQuadTopology => false;
    public static bool supports32bitsIndexBuffer => true;
    public static bool supportsSparseTextures => false;
    public static int supportedRenderTargetCount => 8;
    public static bool supportsSeparatedRenderTargetsBlend => true;
    public static int supportedRandomWriteTargetCount => 8;
    public static bool supportsMultisampledTextures => true;
    public static bool supportsMultisampleAutoResolve => false;
    public static bool supportsTextureWrapMirrorOnce => true;
    public static bool usesReversedZBuffer => true;
    public static bool supportsRenderTextures => true;
    public static bool supportsRenderToCubemap => true;
    public static bool supportsImageEffects => true;
    public static bool supportsVibration => false;
    public static bool supportsGyroscope => false;
    public static bool supportsLocationService => false;
    public static bool supportsAccelerometer => false;
    public static bool supportsAudio => true;
    public static NPOTSupport npotSupport => NPOTSupport.Full;
    public static int maxTextureSize => 8192;
    public static int maxCubemapSize => 8192;
    public static int maxTexture3DSize => 2048;
    public static int maxTextureArraySize => 512;
    public static int maxComputeBufferInputsCompute => 8;
    public static int maxComputeBufferInputsGeometry => 0;
    public static int maxComputeBufferInputsVertex => 0;
    public static int maxComputeBufferInputsFragment => 8;
    public static int maxComputeBufferInputsKernel => 8;
    public static int maxComputeWorkGroupSize => 1024;
    public static int maxComputeWorkGroupSizeX => 1024;
    public static int maxComputeWorkGroupSizeY => 1024;
    public static int maxComputeWorkGroupSizeZ => 64;
    public static int maxConstantBufferSize => 65536;
    public static int graphicsPixelFillrate => 0;

    public static bool SupportsRenderTextureFormat(RenderTextureFormat format) => true;
    public static bool SupportsTextureFormat(TextureFormat format) => true;
    public static bool SupportsBlendingOnRenderTextureFormat(RenderTextureFormat format) => true;
    public static bool SupportsVertexAttributeFormat(VertexAttributeFormat format, int dimension) => true;
    public static bool IsFormatSupported(GraphicsFormat format, FormatUsage usage) => true;

    private static OperatingSystemFamily GetOSFamily()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return OperatingSystemFamily.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return OperatingSystemFamily.MacOSX;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return OperatingSystemFamily.Linux;
        return OperatingSystemFamily.Other;
    }

    private static GraphicsDeviceType GetGraphicsDeviceType()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return GraphicsDeviceType.Direct3D11;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return GraphicsDeviceType.Metal;
        return GraphicsDeviceType.OpenGLCore;
    }

    private static string GetGraphicsDeviceVersion(GraphicsDeviceType type) => type switch
    {
        GraphicsDeviceType.Direct3D11 => "11.0",
        GraphicsDeviceType.Direct3D12 => "12.0",
        GraphicsDeviceType.Vulkan => "Vulkan 1.3",
        GraphicsDeviceType.Metal => "Apple GPU Family 8",
        GraphicsDeviceType.OpenGLCore => "OpenGL 4.5",
        GraphicsDeviceType.OpenGLES2 => "OpenGL ES 2.0",
        GraphicsDeviceType.OpenGLES3 => "OpenGL ES 3.0",
        GraphicsDeviceType.WebGL2 => "WebGL 2.0 (OpenGL ES 3.0)",
        GraphicsDeviceType.Null => "Null",
        _ => "Unknown"
    };
}

public enum OperatingSystemFamily
{
    Other,
    MacOSX,
    Windows,
    Linux
}

public enum DeviceType
{
    Unknown,
    Handheld,
    Console,
    Desktop,
    Television
}

public enum NPOTSupport
{
    None,
    Restricted,
    Full
}
