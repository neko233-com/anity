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

public class Texture2D : Object
{
    private readonly Color[] _pixels;
    private Color32[] _pixels32;
    private bool _isLoaded;
    public int width { get; }
    public int height { get; }
    public string name { get; set; } = string.Empty;
    public TextureFormat format { get; set; }
    public FilterMode filterMode { get; set; }
    public TextureWrapMode wrapMode { get; set; }
    public float mipMapBias { get; set; }

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
    }

    public bool isReadable => true;

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
