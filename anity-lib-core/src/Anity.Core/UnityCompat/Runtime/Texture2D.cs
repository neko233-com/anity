using System;

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
    private readonly Color[] _pixels;
    private Color32[] _pixels32;
    private bool _isLoaded;
    public TextureFormat format { get; set; }
    public override bool isReadable => true;

    public Texture2D(int width, int height) : this(width, height, TextureFormat.RGBA32)
    {
    }

    public Texture2D(int width, int height, TextureFormat format)
    {
        this.width = width;
        this.height = height;
        this.format = format;
        _pixels = new Color[Math.Max(1, width * height)];
        _pixels32 = new Color32[Math.Max(1, width * height)];
        dimension = TextureDimension.Tex2D;
    }

    public void SetPixel(int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return;
        }

        _pixels[y * width + x] = color;
        _pixels32[y * width + x] = color;
    }

    public Color GetPixel(int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return default;
        }

        return _pixels[y * width + x];
    }

    public Color[] GetPixels()
    {
        var clone = new Color[_pixels.Length];
        Array.Copy(_pixels, clone, _pixels.Length);
        return clone;
    }

    public void SetPixels(Color[] colors)
    {
        if (colors == null) throw new ArgumentNullException(nameof(colors));
        if (colors.Length > _pixels.Length)
            throw new ArgumentException("Color array is larger than texture size.");
        Array.Copy(colors, _pixels, colors.Length);
        for (int i = 0; i < colors.Length; i++)
        {
            _pixels32[i] = colors[i];
        }
    }

    public void SetPixels32(Color32[] colors)
    {
        if (colors == null) throw new ArgumentNullException(nameof(colors));
        if (colors.Length > _pixels32.Length)
            throw new ArgumentException("Color32 array is larger than texture size.");
        Array.Copy(colors, _pixels32, colors.Length);
        for (int i = 0; i < colors.Length; i++)
        {
            _pixels[i] = colors[i];
        }
    }

    public Color32[] GetPixels32()
    {
        var clone = new Color32[_pixels32.Length];
        Array.Copy(_pixels32, clone, _pixels32.Length);
        return clone;
    }

    public void Apply()
    {
        // no-op placeholder
    }

    public void Apply(bool updateMipmaps)
    {
        // no-op placeholder
    }

    public void Apply(bool updateMipmaps, bool makeNoLongerReadable)
    {
        // no-op placeholder
    }

    public bool LoadImage(byte[] data)
    {
        if (data == null || data.Length == 0) return false;
        _isLoaded = true;
        return true;
    }

    public byte[] EncodeToPNG()
    {
        // stub
        return Array.Empty<byte>();
    }

    public byte[] EncodeToJPG(int quality = 75)
    {
        // stub
        return Array.Empty<byte>();
    }

    public byte[] GetRawTextureData()
    {
        // stub
        return Array.Empty<byte>();
    }
}
