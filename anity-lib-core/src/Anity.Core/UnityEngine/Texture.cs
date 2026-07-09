using System;

namespace UnityEngine;

public class Texture : Object
{
    public int width { get; protected set; }
    public int height { get; protected set; }
    public string name { get; set; } = string.Empty;
    public FilterMode filterMode { get; set; } = FilterMode.Bilinear;
    public TextureWrapMode wrapMode { get; set; } = TextureWrapMode.Repeat;
    public TextureWrapMode wrapModeU { get; set; } = TextureWrapMode.Repeat;
    public TextureWrapMode wrapModeV { get; set; } = TextureWrapMode.Repeat;
    public TextureWrapMode wrapModeW { get; set; } = TextureWrapMode.Repeat;
    public float mipMapBias { get; set; }
    public int anisoLevel { get; set; } = 1;
    public bool mipmapCount { get; protected set; }
    public TextureDimension dimension { get; set; } = TextureDimension.Tex2D;
    public GraphicsFormat graphicsFormat { get; set; } = GraphicsFormat.R8G8B8A8_UNorm;
    public virtual bool isReadable { get; }
    public int texelSize { get; }
    public Hash128 imageContentsHash { get; set; }

    public Texture()
    {
    }

    public virtual void IncrementUpdateCount()
    {
    }

    public static int GenerateMipsCount(int w, int h)
    {
        int count = 1;
        while (w > 1 || h > 1)
        {
            w = Math.Max(1, w >> 1);
            h = Math.Max(1, h >> 1);
            count++;
        }
        return count;
    }

    public static int CalculateFormatSize(int width, int height, int depth, TextureFormat format)
    {
        int size = width * height * depth;
        switch (format)
        {
            case TextureFormat.Alpha8:
                return size;
            case TextureFormat.RGB24:
                return size * 3;
            case TextureFormat.RGBA32:
            case TextureFormat.ARGB32:
            case TextureFormat.BGRA32:
                return size * 4;
            case TextureFormat.RGB565:
            case TextureFormat.ARGB4444:
                return size * 2;
            case TextureFormat.R16:
            case TextureFormat.RHalf:
                return size * 2;
            case TextureFormat.RGHalf:
                return size * 4;
            case TextureFormat.RGBAHalf:
                return size * 8;
            case TextureFormat.RFloat:
                return size * 4;
            case TextureFormat.RGFloat:
                return size * 8;
            case TextureFormat.RGBAFloat:
                return size * 16;
            case TextureFormat.DXT1:
            case TextureFormat.DXT1Crunched:
                return Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * 8 * depth;
            case TextureFormat.DXT5:
            case TextureFormat.DXT5Crunched:
                return Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * 16 * depth;
            case TextureFormat.ASTC_4x4:
                return Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * 16 * depth;
            case TextureFormat.ASTC_5x5:
                return Math.Max(1, (width + 4) / 5) * Math.Max(1, (height + 4) / 5) * 16 * depth;
            case TextureFormat.ASTC_6x6:
                return Math.Max(1, (width + 5) / 6) * Math.Max(1, (height + 5) / 6) * 16 * depth;
            case TextureFormat.ASTC_8x8:
                return Math.Max(1, (width + 7) / 8) * Math.Max(1, (height + 7) / 8) * 16 * depth;
            case TextureFormat.ASTC_10x10:
                return Math.Max(1, (width + 9) / 10) * Math.Max(1, (height + 9) / 10) * 16 * depth;
            case TextureFormat.ASTC_12x12:
                return Math.Max(1, (width + 11) / 12) * Math.Max(1, (height + 11) / 12) * 16 * depth;
            case TextureFormat.ETC_RGB4:
            case TextureFormat.ETC_RGB4_3DS:
                return Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * 8 * depth;
            case TextureFormat.ETC2_RGB:
                return Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * 8 * depth;
            case TextureFormat.ETC2_RGBA1:
                return Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * 8 * depth;
            case TextureFormat.ETC2_RGBA8:
                return Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * 16 * depth;
            case TextureFormat.PVRTC_RGB2:
            case TextureFormat.PVRTC_RGBA2:
                return Math.Max(1, (width + 7) / 8) * Math.Max(1, (height + 7) / 8) * 32 * depth;
            case TextureFormat.PVRTC_RGB4:
            case TextureFormat.PVRTC_RGBA4:
                return Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * 32 * depth;
            default:
                return size * 4;
        }
    }

    public static bool IsValidNativeTexturePtr(IntPtr ptr)
    {
        return ptr != IntPtr.Zero;
    }

    public IntPtr GetNativeTexturePtr()
    {
        return IntPtr.Zero;
    }
}

public enum TextureDimension
{
    Unknown = -1,
    None = 0,
    Any = 1,
    Tex2D = 2,
    Tex3D = 3,
    Cube = 4,
    Tex2DArray = 5,
    CubeArray = 6,
    Count = 7
}

public enum GraphicsFormat
{
    None = 0,
    R8G8B8A8_UNorm = 1,
    R8G8B8A8_SNorm = 2,
    R8G8B8A8_UInt = 3,
    R8G8B8A8_SInt = 4,
    R16G16B16A16_UNorm = 5,
    R16G16B16A16_SNorm = 6,
    R16G16B16A16_UInt = 7,
    R16G16B16A16_SInt = 8,
    R16G16B16A16_SFloat = 9,
    R32G32B32A32_UInt = 10,
    R32G32B32A32_SInt = 11,
    R32G32B32A32_SFloat = 12,
    R8_UNorm = 13,
    R8_SNorm = 14,
    R8_UInt = 15,
    R8_SInt = 16,
    R16_UNorm = 17,
    R16_SNorm = 18,
    R16_UInt = 19,
    R16_SInt = 20,
    R16_SFloat = 21,
    R32_UInt = 22,
    R32_SInt = 23,
    R32_SFloat = 24,
    R8G8_UNorm = 25,
    R8G8_SNorm = 26,
    R8G8_UInt = 27,
    R8G8_SInt = 28,
    R16G16_UNorm = 29,
    R16G16_SNorm = 30,
    R16G16_UInt = 31,
    R16G16_SInt = 32,
    R16G16_SFloat = 33,
    R32G32_UInt = 34,
    R32G32_SInt = 35,
    R32G32_SFloat = 36,
    B8G8R8A8_UNorm = 37,
    R5G6B5_UNorm = 38,
    B5G6R5_UNorm = 39,
    A4R4G4B4_UNorm = 40,
    ARGB4444 = 41,
    R10G10B10A2_UNorm = 42,
    R10G10B10A2_UInt = 43,
    R11G11B10_UFloat = 44,
    DXT1 = 45,
    DXT3 = 46,
    DXT5 = 47,
    BC4_UNorm = 48,
    BC4_SNorm = 49,
    BC5_UNorm = 50,
    BC5_SNorm = 51,
    BC6H_UFloat = 52,
    BC6H_SFloat = 53,
    BC7 = 54,
    ETC2_RGB = 55,
    ETC2_RGBA1 = 56,
    ETC2_RGBA8 = 57,
    EAC_R_11U = 58,
    EAC_R_11S = 59,
    EAC_RG_11U = 60,
    EAC_RG_11S = 61,
    ASTC_4x4 = 62,
    ASTC_5x5 = 63,
    ASTC_6x6 = 64,
    ASTC_8x8 = 65,
    ASTC_10x10 = 66,
    ASTC_12x12 = 67,
    DepthAuto = 68,
    Depth16 = 69,
    Depth24 = 70,
    Depth32 = 71,
    DepthFloat = 72,
    Depth24_Stencil8 = 73,
    Depth32_Stencil8 = 74,
    Stencil8 = 75,
    ShadowAuto = 76,
    Shadow16 = 77,
    Shadow24 = 78,
    Shadow32 = 79,
    ARGB32 = 80,
    RGB24 = 81,
    RGBA32 = 82,
    Alpha8 = 83,
    RGBAFloat = 84,
    RGBAHalf = 85,
    RGFloat = 86,
    RGHalf = 87,
    RFloat = 88,
    RHalf = 89,
    RInt = 90,
    RGInt = 91,
    RGBAInt = 92,
    Default = 93
}

public struct Hash128
{
    public uint u32_0;
    public uint u32_1;
    public uint u32_2;
    public uint u32_3;

    public Hash128(uint u32_0 = 0, uint u32_1 = 0, uint u32_2 = 0, uint u32_3 = 0)
    {
        this.u32_0 = u32_0;
        this.u32_1 = u32_1;
        this.u32_2 = u32_2;
        this.u32_3 = u32_3;
    }

    public bool isValid => u32_0 != 0 || u32_1 != 0 || u32_2 != 0 || u32_3 != 0;

    public static Hash128 Parse(string hashString)
    {
        return new Hash128();
    }

    public override string ToString()
    {
        return $"{u32_0:x8}{u32_1:x8}{u32_2:x8}{u32_3:x8}";
    }

    public static bool operator ==(Hash128 a, Hash128 b)
    {
        return a.u32_0 == b.u32_0 && a.u32_1 == b.u32_1 && a.u32_2 == b.u32_2 && a.u32_3 == b.u32_3;
    }

    public static bool operator !=(Hash128 a, Hash128 b)
    {
        return !(a == b);
    }

    public override bool Equals(object obj)
    {
        return obj is Hash128 other && this == other;
    }

    public override int GetHashCode()
    {
        return (int)(u32_0 ^ u32_1 ^ u32_2 ^ u32_3);
    }
}