using System.Runtime.InteropServices;

namespace UnityEngine;

/// <summary>
/// Hardware and system information.
/// </summary>
public static class SystemInfo
{
    public static string deviceName => Environment.MachineName;
    public static string deviceModel => "Anity Device";
    public static string deviceUniqueIdentifier => Guid.NewGuid().ToString("N");
    public static string operatingSystem => RuntimeInformation.OSDescription;
    public static OperatingSystemFamily operatingSystemFamily => GetOSFamily();
    public static string processorType => RuntimeInformation.ProcessArchitecture.ToString();
    public static int processorCount => Environment.ProcessorCount;
    public static int processorFrequency => 3000;
    public static int systemMemorySize => 16384;
    public static string graphicsDeviceName => "Anity GPU";
    public static string graphicsDeviceVendor => "Anity";
    public static int graphicsDeviceID => 0;
    public static int graphicsVendorID => 0;
    public static string graphicsDeviceVersion => "1.0";
    public static int graphicsMemorySize => 4096;
    public static int graphicsShaderLevel => 50;
    public static bool graphicsMultiThreaded => false;
    public static bool supportsShadows => true;
    public static bool supportsRawShadowDepthSampling => true;
    public static bool supportsMotionVectors => true;
    public static bool supports3DTextures => true;
    public static bool supports2DArrayTextures => true;
    public static bool supportsCubemapArrayTextures => true;
    public static bool supportsComputeShaders => true;
    public static bool supportsInstancing => true;
    public static bool supportsHardwareQuadTopology => false;
    public static bool supports32bitsIndexBuffer => true;
    public static bool supportsSparseTextures => false;
    public static int supportedRenderTargetCount => 8;
    public static bool supportsSeparatedRenderTargetsBlend => true;
    public static int supportedRandomWriteTargetCount => 8;
    public static bool supportsMultisampledTextures => true;
    public static bool supportsMultisampleAutoResolve => false;
    public static bool supportsTextureWrapMirrorOnce => false;
    public static bool usesReversedZBuffer => true;
    public static bool supportsRenderTextures => true;
    public static bool supportsRenderToCubemap => true;
    public static bool supportsImageEffects => true;
    public static bool supportsVibration => false;
    public static bool supportsGyroscope => false;
    public static bool supportsLocationService => false;
    public static bool supportsAccelerometer => false;
    public static bool supportsAudio => true;
    public static DeviceType deviceType => DeviceType.Desktop;
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
    public static NPOTSupport npotSupport => NPOTSupport.Full;
    public static string copyTextureSupport => "Basic, CopyTexture, DifferentTypes";
    public static int graphicsPixelFillrate => 0;

    public static bool SupportsRenderTextureFormat(RenderTextureFormat format) => true;
    public static bool SupportsTextureFormat(TextureFormat format) => true;
    public static bool SupportsBlendingOnRenderTextureFormat(RenderTextureFormat format) => true;
    public static bool SupportsVertexAttributeFormat(VertexAttributeFormat format, int dimension) => true;
    public static bool IsFormatSupported(UnityEngine.Experimental.Rendering.GraphicsFormat format, UnityEngine.Experimental.Rendering.FormatUsage usage) => true;

    private static OperatingSystemFamily GetOSFamily()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return OperatingSystemFamily.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return OperatingSystemFamily.MacOSX;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return OperatingSystemFamily.Linux;
        return OperatingSystemFamily.Other;
    }
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
