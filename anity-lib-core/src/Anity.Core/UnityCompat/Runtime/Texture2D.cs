using System;
using System.IO;

namespace UnityEngine;

public enum TextureFormat
{
    Alpha8 = 1,
    RGB24 = 3,
    RGBA32 = 4,
    ARGB32 = 5,
    RGB565 = 7,
    DXT1 = 10,
    DXT5 = 12,
    PVRTC_RGB2 = 30,
    PVRTC_RGBA2 = 31,
    PVRTC_RGB4 = 32,
    PVRTC_RGBA4 = 33,
    ETC_RGB4 = 34,
    ETC2_RGB = 45,
    ETC2_RGBA1 = 46,
    ETC2_RGBA8 = 47,
    ASTC_4x4 = 48,
    ASTC_5x5 = 49,
    ASTC_6x6 = 50,
    ASTC_8x8 = 51,
    ASTC_10x10 = 52,
    ASTC_12x12 = 53,
    RGBAFloat = 54,
    RGFloat = 55,
    RFloat = 56,
    RGBAHalf = 57,
    RGHalf = 58,
    RHalf = 59,
    RInt = 60,
    RGInt = 61,
    RGBAInt = 62,
    BC4 = 63,
    BC5 = 64,
    BC6H = 65,
    BC7 = 66,
    DXT1Crunched = 67,
    DXT5Crunched = 68,
    ETC_RGB4_3DS = 69,
    ETC_RGBA8_3DS = 70,
    PVRTC_2BPP_RGB = -127,
    PVRTC_2BPP_RGBA = -127,
    PVRTC_4BPP_RGB = -127,
    PVRTC_4BPP_RGBA = -127,
    BGRA32 = 71,
    R16 = 72,
    ARGB4444 = 73,
    RG16 = 74,
    RGB48 = 75,
    RGBA64 = 76,
    R8 = 77,
    RG32 = 78,
    RGB9E5 = 79,
    RG11B10 = 80,
    R8G8B8A8_UNorm = 81,
    B8G8R8A8_UNorm = 82,
    R10G10B10A2 = 83,
}

public enum FilterMode
{
    Point = 0,
    Bilinear = 1,
    Trilinear = 2,
}

public enum TextureWrapMode
{
    Repeat = 0,
    Clamp = 1,
    Mirror = 2,
    MirrorOnce = 3,
}

public class Texture2D : Texture
{
    private Color[] _pixels;
    private Color32[] _pixels32;
    private bool _isLoaded;
    private bool _mipmapsDirty;
    private bool _isReadable = true;
    private Rect _lastReadRect;
    public TextureFormat format { get; set; }
    public override bool isReadable => _isReadable;
    public int mipmapCount { get; private set; } = 1;

    public Texture2D() : this(4, 4, TextureFormat.RGBA32, false)
    {
    }

    public Texture2D(int width, int height) : this(width, height, TextureFormat.RGBA32, false)
    {
    }

    public Texture2D(int width, int height, TextureFormat format) : this(width, height, format, false)
    {
    }

    public Texture2D(int width, int height, TextureFormat format, bool mipmapChain)
    {
        this.width = width;
        this.height = height;
        this.format = format;
        mipmapCount = mipmapChain ? GenerateMipsCount(width, height) : 1;
        int pixelCount = Math.Max(1, width * height);
        _pixels = new Color[pixelCount];
        _pixels32 = new Color32[pixelCount];
        dimension = TextureDimension.Tex2D;
    }

    public Color GetPixel(int x, int y)
    {
        if (!_isReadable) return Color.clear;
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return Color.clear;
        }
        return _pixels[y * width + x];
    }

    public Color GetPixelBilinear(float u, float v)
    {
        if (!_isReadable) return Color.clear;
        u = u - (int)u;
        v = v - (int)v;
        if (u < 0) u += 1f;
        if (v < 0) v += 1f;
        int x = (int)(u * (width - 1));
        int y = (int)(v * (height - 1));
        return GetPixel(x, y);
    }

    public void SetPixel(int x, int y, Color color)
    {
        if (!_isReadable) return;
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return;
        }
        _pixels[y * width + x] = color;
        _pixels32[y * width + x] = color;
    }

    public Color[] GetPixels()
    {
        if (!_isReadable) return Array.Empty<Color>();
        var clone = new Color[_pixels.Length];
        Array.Copy(_pixels, clone, _pixels.Length);
        return clone;
    }

    public Color[] GetPixels(int miplevel)
    {
        return GetPixels();
    }

    public void SetPixels(Color[] colors)
    {
        if (!_isReadable) return;
        if (colors == null) throw new ArgumentNullException(nameof(colors));
        int count = Math.Min(colors.Length, _pixels.Length);
        Array.Copy(colors, _pixels, count);
        for (int i = 0; i < count; i++)
        {
            _pixels32[i] = colors[i];
        }
    }

    public void SetPixels(Color[] colors, int miplevel)
    {
        SetPixels(colors);
    }

    public void SetPixels32(Color32[] colors)
    {
        if (!_isReadable) return;
        if (colors == null) throw new ArgumentNullException(nameof(colors));
        int count = Math.Min(colors.Length, _pixels32.Length);
        Array.Copy(colors, _pixels32, count);
        for (int i = 0; i < count; i++)
        {
            _pixels[i] = colors[i];
        }
    }

    public void SetPixels32(Color32[] colors, int miplevel)
    {
        SetPixels32(colors);
    }

    public Color32[] GetPixels32()
    {
        if (!_isReadable) return Array.Empty<Color32>();
        var clone = new Color32[_pixels32.Length];
        Array.Copy(_pixels32, clone, _pixels32.Length);
        return clone;
    }

    public Color32[] GetPixels32(int miplevel)
    {
        return GetPixels32();
    }

    public void Apply()
    {
        Apply(true, false);
    }

    public void Apply(bool updateMipmaps)
    {
        Apply(updateMipmaps, false);
    }

    public void Apply(bool updateMipmaps, bool makeNoLongerReadable)
    {
        _isLoaded = true;
        _mipmapsDirty = true;
        if (makeNoLongerReadable)
        {
            _isReadable = false;
        }
    }

    public bool Resize(int width, int height)
    {
        return Resize(width, height, format, false);
    }

    public bool Resize(int width, int height, TextureFormat format, bool hasMipMap)
    {
        this.width = width;
        this.height = height;
        this.format = format;
        int pixelCount = Math.Max(1, width * height);
        _pixels = new Color[pixelCount];
        _pixels32 = new Color32[pixelCount];
        _isReadable = true;
        return true;
    }

    public void Compress(bool highQuality)
    {
        format = highQuality ? TextureFormat.DXT5 : TextureFormat.DXT1;
    }

    public void ReadPixels(Rect source, int destX, int destY)
    {
        ReadPixels(source, destX, destY, true);
    }

    public void ReadPixels(Rect source, int destX, int destY, bool recalculateMipMaps)
    {
        _isLoaded = true;
        _lastReadRect = source;
        _mipmapsDirty = recalculateMipMaps;
        int sx = Math.Max(0, (int)source.x);
        int sy = Math.Max(0, (int)source.y);
        int sw = Math.Min((int)source.width, width - destX);
        int sh = Math.Min((int)source.height, height - destY);
        for (int y = 0; y < sh; y++)
        {
            for (int x = 0; x < sw; x++)
            {
                int srcX = sx + x;
                int srcY = sy + y;
                if (srcX < width && srcY < height)
                    SetPixel(destX + x, destY + y, GetPixel(srcX, srcY));
            }
        }
    }

    public bool LoadImage(byte[] data)
    {
        return LoadImage(data, false);
    }

    public bool LoadImage(byte[] data, bool markNonReadable)
    {
        if (data == null || data.Length == 0) return false;
        _isLoaded = true;
        if (!markNonReadable)
        {
            bool isPng = data.Length > 8 && data[0] == 0x89 && data[1] == 0x50;
            bool isJpeg = data.Length > 2 && data[0] == 0xFF && data[1] == 0xD8;
            if (isPng || isJpeg)
            {
                for (int i = 0; i < _pixels.Length; i++)
                    _pixels[i] = new Color(0.5f, 0.5f, 0.5f, 1f);
            }
        }
        return true;
    }

    public void LoadRawTextureData(byte[] data)
    {
        if (data == null) return;
        int byteCount = _pixels.Length * 4;
        int copyCount = Math.Min(data.Length, byteCount);
        for (int i = 0; i < copyCount / 4; i++)
        {
            _pixels[i] = new Color(
                data[i * 4 + 0] / 255f,
                data[i * 4 + 1] / 255f,
                data[i * 4 + 2] / 255f,
                data[i * 4 + 3] / 255f
            );
            _pixels32[i] = _pixels[i];
        }
    }

    public byte[] EncodeToPNG()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((byte)0x89); w.Write((byte)'P'); w.Write((byte)'N'); w.Write((byte)'G');
        w.Write((byte)0x0D); w.Write((byte)0x0A); w.Write((byte)0x1A); w.Write((byte)0x0A);
        w.Write((uint)13); w.Write((byte)'I'); w.Write((byte)'H'); w.Write((byte)'D'); w.Write((byte)'R');
        w.Write(ToBigEndian((uint)width));
        w.Write(ToBigEndian((uint)height));
        w.Write((byte)8); w.Write((byte)6); w.Write((byte)0); w.Write((byte)0); w.Write((byte)0);
        uint crc = Crc32(ms.ToArray(), 12, 17);
        w.Write(ToBigEndian(crc));
        w.Write(ToBigEndian((uint)(_pixels.Length * 4 + 4)));
        w.Write((byte)'I'); w.Write((byte)'D'); w.Write((byte)'A'); w.Write((byte)'T');
        var rawData = GetRawTextureData();
        w.Write(rawData);
        w.Write(ToBigEndian(Crc32(ms.ToArray(), ms.Position - rawData.Length - 4, rawData.Length + 4)));
        w.Write((uint)0);
        w.Write((byte)'I'); w.Write((byte)'E'); w.Write((byte)'N'); w.Write((byte)'D');
        w.Write(ToBigEndian(Crc32(ms.ToArray(), ms.Position - 4, 4)));
        return ms.ToArray();
    }

    public byte[] EncodeToJPG()
    {
        return EncodeToJPG(75);
    }

    public byte[] EncodeToJPG(int quality)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((byte)0xFF); w.Write((byte)0xD8);
        w.Write((byte)0xFF); w.Write((byte)0xE0);
        w.Write((ushort)16); w.Write((byte)'J'); w.Write((byte)'F'); w.Write((byte)'I'); w.Write((byte)'F'); w.Write((byte)0);
        w.Write((byte)1); w.Write((byte)1); w.Write((byte)0); w.Write((ushort)1); w.Write((ushort)1); w.Write((byte)0); w.Write((byte)0);
        byte q = (byte)Math.Clamp(quality, 1, 100);
        w.Write((byte)0xFF); w.Write((byte)0xDB);
        w.Write((ushort)67); w.Write((byte)0);
        for (int i = 0; i < 64; i++) w.Write(q);
        w.Write((byte)0xFF); w.Write((byte)0xC0);
        w.Write((ushort)17); w.Write((byte)8);
        w.Write(ToBigEndian((ushort)height));
        w.Write(ToBigEndian((ushort)width));
        w.Write((byte)3);
        w.Write((byte)1); w.Write((byte)0x11); w.Write((byte)0);
        w.Write((byte)2); w.Write((byte)0x11); w.Write((byte)0);
        w.Write((byte)3); w.Write((byte)0x11); w.Write((byte)0);
        w.Write((byte)0xFF); w.Write((byte)0xDA);
        w.Write((ushort)12); w.Write((byte)3);
        w.Write((byte)1); w.Write((byte)0);
        w.Write((byte)2); w.Write((byte)0x11); w.Write((byte)0);
        w.Write((byte)3); w.Write((byte)0x11); w.Write((byte)0);
        w.Write((byte)0); w.Write((byte)0x3F); w.Write((byte)0);
        var rawData = GetRawTextureData();
        w.Write(rawData);
        w.Write((byte)0xFF); w.Write((byte)0xD9);
        return ms.ToArray();
    }

    public byte[] GetRawTextureData()
    {
        var result = new byte[_pixels.Length * 4];
        for (int i = 0; i < _pixels.Length; i++)
        {
            result[i * 4 + 0] = (byte)(Math.Clamp(_pixels[i].r, 0f, 1f) * 255f);
            result[i * 4 + 1] = (byte)(Math.Clamp(_pixels[i].g, 0f, 1f) * 255f);
            result[i * 4 + 2] = (byte)(Math.Clamp(_pixels[i].b, 0f, 1f) * 255f);
            result[i * 4 + 3] = (byte)(Math.Clamp(_pixels[i].a, 0f, 1f) * 255f);
        }
        return result;
    }

    private static byte[] ToBigEndian(uint value)
    {
        return new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };
    }

    private static byte[] ToBigEndian(ushort value)
    {
        return new[] { (byte)(value >> 8), (byte)value };
    }

    private static uint Crc32(byte[] data, long offset, int length)
    {
        uint crc = 0xFFFFFFFF;
        for (long i = offset; i < offset + length && i < data.Length; i++)
        {
            crc ^= data[i];
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 1) != 0)
                    crc = (crc >> 1) ^ 0xEDB88320u;
                else
                    crc = crc >> 1;
            }
        }
        return ~crc;
    }

    private static int GenerateMipsCount(int w, int h)
    {
        int count = 1;
        while (w > 1 || h > 1) { w = Math.Max(1, w / 2); h = Math.Max(1, h / 2); count++; }
        return count;
    }

    private static int _copyCount;
    public static void CopyTexture(Texture src, Texture dst) { _copyCount++; }
    public static void CopyTexture(Texture src, int srcElement, int srcMip, Texture dst, int dstElement, int dstMip) { _copyCount++; }
    public static void CopyTexture(Texture src, int srcElement, int srcMip, int srcX, int srcY, int srcWidth, int srcHeight, Texture dst, int dstElement, int dstMip, int dstX, int dstY) { _copyCount++; }
}
