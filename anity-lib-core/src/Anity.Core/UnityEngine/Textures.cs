using System;

namespace UnityEngine;

public class Texture3D : Texture
{
    public int depth { get; private set; }
    public TextureFormat format { get; set; }

    public Texture3D(int width, int height, int depth, TextureFormat format, bool mipmap)
    {
        this.width = width;
        this.height = height;
        this.depth = depth;
        this.format = format;
        dimension = TextureDimension.Tex3D;
    }

    public Texture3D(int width, int height, int depth, TextureFormat format, bool mipmap, bool linear)
        : this(width, height, depth, format, mipmap)
    {
        _ = linear;
    }

    public override bool isReadable => true;

    public Color GetPixel(int x, int y, int z)
    {
        return default;
    }

    public void SetPixel(int x, int y, int z, Color color)
    {
    }

    public Color[] GetPixels()
    {
        return Array.Empty<Color>();
    }

    public Color[] GetPixels(int mipLevel)
    {
        return Array.Empty<Color>();
    }

    public void SetPixels(Color[] colors)
    {
    }

    public void SetPixels(Color[] colors, int mipLevel)
    {
    }

    public void Apply()
    {
    }

    public void Apply(bool updateMipmaps)
    {
    }

    public void Apply(bool updateMipmaps, bool makeNoLongerReadable)
    {
    }
}

public class Texture2DArray : Texture
{
    public int depth { get; private set; }
    public TextureFormat format { get; set; }
    public bool useMipMap { get; private set; }

    public Texture2DArray(int width, int height, int depth, TextureFormat format, bool mipmap)
    {
        this.width = width;
        this.height = height;
        this.depth = depth;
        this.format = format;
        this.useMipMap = mipmap;
        dimension = TextureDimension.Tex2DArray;
    }

    public Texture2DArray(int width, int height, int depth, TextureFormat format, bool mipmap, bool linear)
        : this(width, height, depth, format, mipmap)
    {
        _ = linear;
    }

    public override bool isReadable => true;

    public Color[] GetPixels(int arrayElement, int miplevel = 0)
    {
        return Array.Empty<Color>();
    }

    public void SetPixels(Color[] pixels, int arrayElement, int miplevel = 0)
    {
    }

    public void Apply()
    {
    }

    public void Apply(bool updateMipmaps)
    {
    }

    public void Apply(bool updateMipmaps, bool makeNoLongerReadable)
    {
    }
}

public class CubemapArray : Texture
{
    public int cubemapCount { get; private set; }
    public TextureFormat format { get; set; }
    public bool useMipMap { get; private set; }

    public CubemapArray(int faceSize, int cubemapCount, TextureFormat format, bool mipmap)
    {
        this.width = faceSize;
        this.height = faceSize;
        this.cubemapCount = cubemapCount;
        this.format = format;
        this.useMipMap = mipmap;
        dimension = TextureDimension.CubeArray;
    }

    public CubemapArray(int faceSize, int cubemapCount, TextureFormat format, bool mipmap, bool linear)
        : this(faceSize, cubemapCount, format, mipmap)
    {
        _ = linear;
    }

    public override bool isReadable => true;

    public Color[] GetPixels(CubemapFace face, int arrayElement, int miplevel = 0)
    {
        return Array.Empty<Color>();
    }

    public void SetPixels(Color[] pixels, CubemapFace face, int arrayElement, int miplevel = 0)
    {
    }

    public void Apply()
    {
    }

    public void Apply(bool updateMipmaps)
    {
    }

    public void Apply(bool updateMipmaps, bool makeNoLongerReadable)
    {
    }
}

public enum CubemapFace
{
    PositiveX = 0,
    NegativeX = 1,
    PositiveY = 2,
    NegativeY = 3,
    PositiveZ = 4,
    NegativeZ = 5,
    Unknown = 6
}

public enum RenderTextureFormat
{
    ARGB32 = 0,
    Depth = 1,
    ARGBHalf = 2,
    Shadowmap = 3,
    RGB565 = 4,
    ARGB4444 = 5,
    ARGB1555 = 6,
    Default = 7,
    ARGB2101010 = 8,
    DefaultHDR = 9,
    ARGB64 = 10,
    ARGBFloat = 11,
    RGFloat = 12,
    RGHalf = 13,
    RFloat = 14,
    RHalf = 15,
    R8 = 16,
    ARGBInt = 17,
    RGInt = 18,
    RInt = 19,
    RGBAUShort = 20,
    BGRA32 = 22,
    RGB111110Float = 23,
    RG32 = 25,
    RGBA64 = 26,
    R16 = 27,
    BGRA10101010_XR = 28,
    BGR101010_XR = 29,
    R8_SRGB = 30,
    RG8_SRGB = 31,
    RGB8_SRGB = 32,
    RGBA8_SRGB = 33,
    BGRA8_SRGB = 34,
    Alpha8 = 35,
}

public enum RenderTextureReadWrite
{
    Default = 0,
    Linear = 1,
    sRGB = 2
}

public enum RenderTextureMemoryless
{
    None = 0,
    Color = 1,
    Depth = 2,
    MSV = 4,
}

public enum VRTextureUsage
{
    None = 0,
    OneEye = 1,
    TwoEyes = 2,
    DeviceSpecific = 3,
}

public enum AntiAliasing
{
    None = 1,
    _2Samples = 2,
    _4Samples = 4,
    _8Samples = 8,
}

public enum Dimension
{
    None = 0,
    Tex2D = 2,
    Tex3D = 3,
    Cube = 4,
    Tex2DArray = 5,
    CubeArray = 6,
}

public class RenderTextureDescriptor
{
    public int width;
    public int height;
    public RenderTextureFormat colorFormat;
    public int depthBufferBits;
    public int mipCount;
    public int volumeDepth;
    public int msaaSamples;
    public TextureDimension dimension;
    public bool sRGB;
    public bool useMipMap;
    public bool autoGenerateMips;
    public bool enableRandomWrite;
    public int shadowSamplingMode;
    public RenderTextureMemoryless memoryless;
    public VRTextureUsage vrUsage;
    public int eyeTextureDesc;

    public RenderTextureDescriptor(int width, int height)
    {
        this.width = width;
        this.height = height;
        colorFormat = RenderTextureFormat.Default;
        depthBufferBits = 0;
        mipCount = -1;
        volumeDepth = 1;
        msaaSamples = 1;
        dimension = TextureDimension.Tex2D;
        sRGB = QualitySettings.activeColorSpace == ColorSpace.Gamma;
        useMipMap = false;
        autoGenerateMips = true;
        enableRandomWrite = false;
        shadowSamplingMode = 0;
        memoryless = RenderTextureMemoryless.None;
        vrUsage = VRTextureUsage.None;
        eyeTextureDesc = 0;
    }

    public RenderTextureDescriptor(int width, int height, RenderTextureFormat colorFormat)
        : this(width, height)
    {
        this.colorFormat = colorFormat;
    }

    public RenderTextureDescriptor(int width, int height, RenderTextureFormat colorFormat, int depthBufferBits)
        : this(width, height, colorFormat)
    {
        this.depthBufferBits = depthBufferBits;
    }
}

public static class QualitySettings
{
    public static ColorSpace activeColorSpace { get; set; } = ColorSpace.Gamma;
    public static int antiAliasing { get; set; } = 1;
    public static float shadowDistance { get; set; } = 40f;
    public static ShadowQuality shadows { get; set; } = ShadowQuality.All;
    public static string currentQualityLevel { get; set; } = "Medium";
    public static int masterTextureLimit { get; set; } = 0;
    public static int maxQueuedFrames { get; set; } = 2;
    public static int vSyncCount { get; set; } = 1;
    public static int pixelLightCount { get; set; } = 2;
    public static bool softVegetation { get; set; } = true;
    public static bool realtimeReflectionProbes { get; set; } = true;
    public static bool billboardsFaceCameraPosition { get; set; } = true;
    public static float lodBias { get; set; } = 2f;
    public static int maximumLODLevel { get; set; } = 0;
    public static bool streamingMipmapsActive { get; set; }
    public static float streamingMipmapsAddAllCamerasMemoryBudget { get; set; } = 512f;

    public static int GetQualityLevel() => 2;
    public static void SetQualityLevel(int index, bool applyExpensiveChanges = false) { }
}

public enum ColorSpace
{
    Uninitialized = -1,
    Gamma = 0,
    Linear = 1,
}

public enum ShadowQuality
{
    Disable = 0,
    HardOnly = 1,
    All = 2,
}