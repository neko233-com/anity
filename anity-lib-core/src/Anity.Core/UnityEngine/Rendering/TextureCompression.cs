using System;

namespace UnityEngine.Rendering;

/// <summary>
/// Block-compressed texture helpers: DXT/BC, ETC/ETC2, ASTC (and PVRTC metadata).
/// Provides block size, byte size, platform support matrix, and soft-compress to block buffers.
/// </summary>
public enum TextureCompressionFamily
{
    Uncompressed,
    DXT,      // BC1/BC3 (DXT1/DXT5) — desktop / console
    BC,       // BC4/BC5/BC6H/BC7
    ETC,      // ETC1 / ETC2 — Android GLES
    ASTC,     // Adaptive Scalable Texture Compression — iOS Metal / modern Android Vulkan
    PVRTC,    // PowerVR (legacy iOS)
    ATC       // Qualcomm ATC (legacy)
}

public static class TextureCompressionUtility
{
    public static TextureCompressionFamily GetFamily(TextureFormat format) => format switch
    {
        TextureFormat.DXT1 or TextureFormat.DXT5 or TextureFormat.DXT1Crunched or TextureFormat.DXT5Crunched
            => TextureCompressionFamily.DXT,
        TextureFormat.BC4 or TextureFormat.BC5 or TextureFormat.BC6H or TextureFormat.BC7
            => TextureCompressionFamily.BC,
        TextureFormat.ETC_RGB4 or TextureFormat.ETC2_RGB or TextureFormat.ETC2_RGBA8
            => TextureCompressionFamily.ETC,
        TextureFormat.ASTC_4x4 or TextureFormat.ASTC_5x5 or TextureFormat.ASTC_6x6
            or TextureFormat.ASTC_8x8 or TextureFormat.ASTC_10x10 or TextureFormat.ASTC_12x12
            or TextureFormat.ASTC_HDR_4x4
            => TextureCompressionFamily.ASTC,
        TextureFormat.PVRTC_RGB2 or TextureFormat.PVRTC_RGBA2 or TextureFormat.PVRTC_RGB4 or TextureFormat.PVRTC_RGBA4
            => TextureCompressionFamily.PVRTC,
        TextureFormat.ATC_RGB4
            => TextureCompressionFamily.ATC,
        _ => TextureCompressionFamily.Uncompressed
    };

    public static bool IsCompressed(TextureFormat format) =>
        GetFamily(format) != TextureCompressionFamily.Uncompressed;

    public static void GetBlockSize(TextureFormat format, out int blockWidth, out int blockHeight, out int blockBytes)
    {
        blockWidth = 4;
        blockHeight = 4;
        blockBytes = 16;

        switch (format)
        {
            case TextureFormat.DXT1:
            case TextureFormat.DXT1Crunched:
            case TextureFormat.BC4:
            case TextureFormat.ETC_RGB4:
            case TextureFormat.ETC2_RGB:
            case TextureFormat.ATC_RGB4:
                blockBytes = 8;
                break;
            case TextureFormat.DXT5:
            case TextureFormat.DXT5Crunched:
            case TextureFormat.BC5:
            case TextureFormat.BC6H:
            case TextureFormat.BC7:
            case TextureFormat.ETC2_RGBA8:
                blockBytes = 16;
                break;
            case TextureFormat.ASTC_4x4:
            case TextureFormat.ASTC_HDR_4x4:
                blockWidth = 4; blockHeight = 4; blockBytes = 16;
                break;
            case TextureFormat.ASTC_5x5:
                blockWidth = 5; blockHeight = 5; blockBytes = 16;
                break;
            case TextureFormat.ASTC_6x6:
                blockWidth = 6; blockHeight = 6; blockBytes = 16;
                break;
            case TextureFormat.ASTC_8x8:
                blockWidth = 8; blockHeight = 8; blockBytes = 16;
                break;
            case TextureFormat.ASTC_10x10:
                blockWidth = 10; blockHeight = 10; blockBytes = 16;
                break;
            case TextureFormat.ASTC_12x12:
                blockWidth = 12; blockHeight = 12; blockBytes = 16;
                break;
            case TextureFormat.PVRTC_RGB2:
            case TextureFormat.PVRTC_RGBA2:
                blockWidth = 8; blockHeight = 4; blockBytes = 8;
                break;
            case TextureFormat.PVRTC_RGB4:
            case TextureFormat.PVRTC_RGBA4:
                blockWidth = 4; blockHeight = 4; blockBytes = 8;
                break;
            default:
                blockWidth = 1; blockHeight = 1;
                blockBytes = BytesPerPixelUncompressed(format);
                break;
        }
    }

    public static int CalculateImageSize(int width, int height, TextureFormat format)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        GetBlockSize(format, out int bw, out int bh, out int blockBytes);
        if (!IsCompressed(format))
            return width * height * blockBytes;

        int blocksX = (width + bw - 1) / bw;
        int blocksY = (height + bh - 1) / bh;
        return blocksX * blocksY * blockBytes;
    }

    public static TextureFormat GetDefaultFormatForPlatform(RuntimePlatform platform, bool hasAlpha, bool highQuality = true)
    {
        return platform switch
        {
            RuntimePlatform.IPhonePlayer or RuntimePlatform.tvOS =>
                highQuality
                    ? (hasAlpha ? TextureFormat.ASTC_4x4 : TextureFormat.ASTC_6x6)
                    : (hasAlpha ? TextureFormat.ASTC_8x8 : TextureFormat.ASTC_10x10),
            RuntimePlatform.Android =>
                // Android + Vulkan preferred path: ASTC when available, else ETC2
                highQuality
                    ? (hasAlpha ? TextureFormat.ASTC_4x4 : TextureFormat.ETC2_RGB)
                    : (hasAlpha ? TextureFormat.ETC2_RGBA8 : TextureFormat.ETC_RGB4),
            RuntimePlatform.WebGLPlayer =>
                hasAlpha ? TextureFormat.DXT5 : TextureFormat.DXT1,
            RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor
                or RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor
                or RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxEditor =>
                hasAlpha ? TextureFormat.DXT5 : TextureFormat.DXT1,
            _ => hasAlpha ? TextureFormat.RGBA32 : TextureFormat.RGB24
        };
    }

    public static TextureFormat GetDefaultFormatForGraphicsAPI(GraphicsDeviceType api, bool hasAlpha, bool highQuality = true)
    {
        return api switch
        {
            GraphicsDeviceType.Metal =>
                highQuality
                    ? (hasAlpha ? TextureFormat.ASTC_4x4 : TextureFormat.ASTC_6x6)
                    : TextureFormat.ASTC_8x8,
            GraphicsDeviceType.Vulkan =>
                highQuality
                    ? (hasAlpha ? TextureFormat.ASTC_4x4 : TextureFormat.ETC2_RGB)
                    : (hasAlpha ? TextureFormat.ETC2_RGBA8 : TextureFormat.ETC_RGB4),
            GraphicsDeviceType.OpenGLES3 =>
                hasAlpha ? TextureFormat.ETC2_RGBA8 : TextureFormat.ETC2_RGB,
            GraphicsDeviceType.OpenGLES2 =>
                hasAlpha ? TextureFormat.RGBA32 : TextureFormat.ETC_RGB4,
            GraphicsDeviceType.Direct3D11 or GraphicsDeviceType.Direct3D12
                or GraphicsDeviceType.OpenGLCore or GraphicsDeviceType.WebGL2 =>
                hasAlpha ? TextureFormat.DXT5 : TextureFormat.DXT1,
            _ => hasAlpha ? TextureFormat.RGBA32 : TextureFormat.RGB24
        };
    }

    /// <summary>
    /// Whether the active (or given) graphics API can sample this compressed format natively.
    /// </summary>
    public static bool IsFormatSupportedOnAPI(TextureFormat format, GraphicsDeviceType api)
    {
        var family = GetFamily(format);
        if (family == TextureCompressionFamily.Uncompressed) return true;

        return api switch
        {
            GraphicsDeviceType.Metal =>
                family is TextureCompressionFamily.ASTC or TextureCompressionFamily.PVRTC or TextureCompressionFamily.ETC,
            GraphicsDeviceType.Vulkan =>
                family is TextureCompressionFamily.ASTC or TextureCompressionFamily.ETC
                    or TextureCompressionFamily.DXT or TextureCompressionFamily.BC,
            GraphicsDeviceType.OpenGLES3 =>
                family is TextureCompressionFamily.ETC or TextureCompressionFamily.ASTC,
            GraphicsDeviceType.OpenGLES2 =>
                family is TextureCompressionFamily.ETC or TextureCompressionFamily.PVRTC,
            GraphicsDeviceType.Direct3D11 or GraphicsDeviceType.Direct3D12 =>
                family is TextureCompressionFamily.DXT or TextureCompressionFamily.BC,
            GraphicsDeviceType.OpenGLCore =>
                family is TextureCompressionFamily.DXT or TextureCompressionFamily.BC or TextureCompressionFamily.ETC,
            GraphicsDeviceType.WebGL2 =>
                family is TextureCompressionFamily.DXT or TextureCompressionFamily.ETC,
            _ => family is TextureCompressionFamily.DXT
        };
    }

    /// <summary>
    /// Soft block-compress: writes a correctly sized block buffer (not bit-exact GPU encoder).
    /// Suitable for import pipeline sizing, AssetBundle planning, and tests.
    /// </summary>
    public static byte[] Compress(Color32[] pixels, int width, int height, TextureFormat format)
    {
        int size = CalculateImageSize(width, height, format);
        var result = new byte[size];
        if (pixels == null || pixels.Length == 0 || !IsCompressed(format))
            return result;

        // Prefer anity-native C++ block compress when available
        if (Anity.Core.Runtime.Native.AnityNative.Available)
        {
            try
            {
                var rgba = new byte[width * height * 4];
                for (int i = 0; i < pixels.Length && i < width * height; i++)
                {
                    rgba[i * 4 + 0] = pixels[i].r;
                    rgba[i * 4 + 1] = pixels[i].g;
                    rgba[i * 4 + 2] = pixels[i].b;
                    rgba[i * 4 + 3] = pixels[i].a;
                }
                int fmt = (int)format;
                if (Anity.Core.Runtime.Native.AnityNative.Texture_CompressRGBA8(rgba, width, height, fmt, result, result.Length)
                    == Anity.Core.Runtime.Native.AnityNative.Result.Ok)
                    return result;
            }
            catch
            {
                Anity.Core.Runtime.Native.AnityNative.MarkUnavailable();
            }
        }

        GetBlockSize(format, out int bw, out int bh, out int blockBytes);
        int blocksX = (width + bw - 1) / bw;
        int blocksY = (height + bh - 1) / bh;
        int bi = 0;

        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                // Average color of block as endpoint proxy
                int r = 0, g = 0, b = 0, a = 0, count = 0;
                for (int y = 0; y < bh; y++)
                {
                    int py = by * bh + y;
                    if (py >= height) break;
                    for (int x = 0; x < bw; x++)
                    {
                        int px = bx * bw + x;
                        if (px >= width) break;
                        var c = pixels[py * width + px];
                        r += c.r; g += c.g; b += c.b; a += c.a;
                        count++;
                    }
                }
                if (count == 0) count = 1;
                r /= count; g /= count; b /= count; a /= count;

                int offset = bi * blockBytes;
                if (offset + blockBytes > result.Length) break;

                // Pack a simple color block header (encoder stub)
                result[offset] = (byte)r;
                result[offset + 1] = (byte)g;
                if (blockBytes >= 4)
                {
                    result[offset + 2] = (byte)b;
                    result[offset + 3] = (byte)a;
                }
                if (blockBytes >= 8)
                {
                    // DXT1/ETC color0/color1 style second endpoint
                    result[offset + 4] = (byte)(r / 2);
                    result[offset + 5] = (byte)(g / 2);
                    result[offset + 6] = (byte)(b / 2);
                    result[offset + 7] = (byte)a;
                }
                bi++;
            }
        }

        return result;
    }

    public static byte[] Compress(Texture2D texture, TextureFormat format)
    {
        if (texture == null) return Array.Empty<byte>();
        var pixels = texture.GetPixels32();
        return Compress(pixels, texture.width, texture.height, format);
    }

    public static UnityEngine.Experimental.Rendering.GraphicsFormat ToGraphicsFormat(TextureFormat format) => format switch
    {
        TextureFormat.DXT1 => UnityEngine.Experimental.Rendering.GraphicsFormat.BC1_Rgb_SRGB,
        TextureFormat.DXT5 => UnityEngine.Experimental.Rendering.GraphicsFormat.BC3_SRGB,
        TextureFormat.BC7 => UnityEngine.Experimental.Rendering.GraphicsFormat.BC7_SRGB,
        TextureFormat.ETC2_RGB => UnityEngine.Experimental.Rendering.GraphicsFormat.ETC2_R8G8B8_SRGB,
        TextureFormat.ETC2_RGBA8 => UnityEngine.Experimental.Rendering.GraphicsFormat.ETC2_R8G8B8A8_SRGB,
        TextureFormat.ASTC_4x4 => UnityEngine.Experimental.Rendering.GraphicsFormat.ASTC_4x4_SRGB,
        TextureFormat.ASTC_6x6 => UnityEngine.Experimental.Rendering.GraphicsFormat.ASTC_6x6_SRGB,
        TextureFormat.ASTC_8x8 => UnityEngine.Experimental.Rendering.GraphicsFormat.ASTC_8x8_SRGB,
        TextureFormat.RGBA32 => UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SRGB,
        _ => UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm
    };

    private static int BytesPerPixelUncompressed(TextureFormat format) => format switch
    {
        TextureFormat.Alpha8 or TextureFormat.R8 => 1,
        TextureFormat.RGB565 or TextureFormat.RGBA4444 or TextureFormat.ARGB4444 or TextureFormat.R16 or TextureFormat.RG16 => 2,
        TextureFormat.RGB24 => 3,
        TextureFormat.RGBA32 or TextureFormat.ARGB32 or TextureFormat.BGRA32 or TextureFormat.RFloat => 4,
        TextureFormat.RGBAHalf => 8,
        TextureFormat.RGBAFloat => 16,
        _ => 4
    };
}
