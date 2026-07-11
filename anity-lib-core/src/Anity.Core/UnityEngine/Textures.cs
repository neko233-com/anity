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

public enum AntiAliasing
{
    None = 1,
    _2Samples = 2,
    _4Samples = 4,
    _8Samples = 8,
}

public static class QualitySettings
{
    public static ColorSpace activeColorSpace { get; set; } = ColorSpace.Gamma;
    public static int antiAliasing { get; set; } = 1;
    public static float shadowDistance { get; set; } = 40f;
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
