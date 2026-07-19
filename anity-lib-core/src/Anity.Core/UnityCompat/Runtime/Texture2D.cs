using System;
using System.Collections.Generic;
using System.IO;

namespace UnityEngine;

public enum TextureFormat
{
    Alpha8 = 1,
    ARGB4444 = 2,
    RGB24 = 3,
    RGBA32 = 4,
    ARGB32 = 5,
    RGB565 = 7,
    R16 = 9,
    DXT1 = 10,
    DXT5 = 12,
    RGBA4444 = 13,
    BGRA32 = 14,
    RHalf = 15,
    RGHalf = 16,
    RGBAHalf = 17,
    RFloat = 18,
    RGFloat = 19,
    RGBAFloat = 20,
    YUY2 = 21,
    RGB9e5Float = 22,
    RGB9E5 = 22,
    BC6H = 24,
    BC7 = 25,
    BC4 = 26,
    BC5 = 27,
    DXT1Crunched = 28,
    DXT5Crunched = 29,
    PVRTC_RGB2 = 30,
    PVRTC_RGBA2 = 31,
    PVRTC_RGB4 = 32,
    PVRTC_RGBA4 = 33,
    ETC_RGB4 = 34,
    ETC_RGB4_3DS = 44,
    ATC_RGB4 = 35,
    ATC_RGBA8 = 36,
    ETC2_RGB = 45,
    ETC2_RGBA1 = 46,
    ETC2_RGBA8 = 47,
    EAC_R = 48,
    EAC_R_SIGNED = 49,
    EAC_RG = 50,
    EAC_RG_SIGNED = 51,
    ASTC_4x4 = 52,
    ASTC_5x5 = 53,
    ASTC_6x6 = 54,
    ASTC_8x8 = 55,
    ASTC_10x10 = 56,
    ASTC_12x12 = 57,
    // HDR ASTC (Unity 2022+)
    ASTC_HDR_4x4 = 66,
    ASTC_HDR_5x5 = 67,
    ASTC_HDR_6x6 = 68,
    ASTC_HDR_8x8 = 69,
    ASTC_HDR_10x10 = 70,
    ASTC_HDR_12x12 = 71,
    RG16 = 62,
    R8 = 63,
    RG32 = 72,
    RGB48 = 73,
    RGBA64 = 74,
    R8G8B8A8_SRGB = 75,
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

public partial class Texture2D : Texture
{
    private Color[] _pixels;
    private Color32[] _pixels32;
    private Color[][] _mipPixels = Array.Empty<Color[]>();
    private Color32[][] _mipPixels32 = Array.Empty<Color32[]>();
    private bool _isLoaded;
    private bool _mipmapsDirty;
    private bool _isReadable = true;
    private Rect _lastReadRect;
    private bool _linear;
    public TextureFormat format { get; set; }
    public override bool isReadable => _isReadable;

    private static Texture2D _whiteTexture;
    private static Texture2D _blackTexture;
    private static Texture2D _redTexture;
    private static Texture2D _normalTexture;

    public static Texture2D whiteTexture
    {
        get
        {
            if (_whiteTexture == null)
            {
                _whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _whiteTexture.SetPixel(0, 0, Color.white);
                _whiteTexture.Apply(false, true);
            }
            return _whiteTexture;
        }
    }

    public static Texture2D blackTexture
    {
        get
        {
            if (_blackTexture == null)
            {
                _blackTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _blackTexture.SetPixel(0, 0, Color.black);
                _blackTexture.Apply(false, true);
            }
            return _blackTexture;
        }
    }

    public static Texture2D redTexture
    {
        get
        {
            if (_redTexture == null)
            {
                _redTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _redTexture.SetPixel(0, 0, Color.red);
                _redTexture.Apply(false, true);
            }
            return _redTexture;
        }
    }

    public static Texture2D normalTexture
    {
        get
        {
            if (_normalTexture == null)
            {
                _normalTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
                _normalTexture.SetPixel(0, 0, new Color(0.5f, 0.5f, 1f, 1f));
                _normalTexture.Apply(false, true);
            }
            return _normalTexture;
        }
    }

    public Texture2D() : this(4, 4, TextureFormat.RGBA32, false, false)
    {
    }

    public Texture2D(int width, int height) : this(width, height, TextureFormat.RGBA32, false, false)
    {
    }

    public Texture2D(int width, int height, TextureFormat format) : this(width, height, format, false, false)
    {
    }

    public Texture2D(int width, int height, TextureFormat format, bool mipmapChain) : this(width, height, format, mipmapChain, false)
    {
    }

    public Texture2D(int width, int height, TextureFormat format, int mipCount, bool linear)
    {
        this.width = width;
        this.height = height;
        this.format = format;
        _linear = linear;
        mipmapCount = Math.Min(mipCount > 0 ? mipCount : 1, GenerateMipsCount(width, height));
        InitializeMipStorage();
        dimension = TextureDimension.Tex2D;
    }

    public Texture2D(int width, int height, TextureFormat format, bool mipmapChain, bool linear)
    {
        this.width = width;
        this.height = height;
        this.format = format;
        _linear = linear;
        mipmapCount = mipmapChain ? GenerateMipsCount(width, height) : 1;
        InitializeMipStorage();
        dimension = TextureDimension.Tex2D;
    }

    public bool linear => _linear;

    public Color GetPixel(int x, int y)
        => GetPixel(x, y, 0);

    public Color GetPixel(int x, int y, [Internal.DefaultValue("0")] int mipLevel)
    {
        if (!_isReadable) return Color.clear;
        if (!TryGetMip(mipLevel, out Color[] pixels, out _, out int mipWidth, out int mipHeight) ||
            x < 0 || y < 0 || x >= mipWidth || y >= mipHeight)
        {
            return Color.clear;
        }
        return pixels[y * mipWidth + x];
    }

    public Color GetPixelBilinear(float u, float v)
        => GetPixelBilinear(u, v, 0);

    public Color GetPixelBilinear(float u, float v, [Internal.DefaultValue("0")] int mipLevel)
    {
        if (!_isReadable) return Color.clear;
        if (!TryGetMip(mipLevel, out _, out _, out int mipWidth, out int mipHeight))
            return Color.clear;
        u = u - (float)Math.Floor(u);
        v = v - (float)Math.Floor(v);
        if (u < 0) u += 1f;
        if (v < 0) v += 1f;

        float fx = u * (mipWidth - 1);
        float fy = v * (mipHeight - 1);
        int x0 = (int)Math.Floor(fx);
        int y0 = (int)Math.Floor(fy);
        int x1 = Math.Min(x0 + 1, mipWidth - 1);
        int y1 = Math.Min(y0 + 1, mipHeight - 1);
        float tx = fx - x0;
        float ty = fy - y0;

        Color c00 = GetPixel(x0, y0, mipLevel);
        Color c10 = GetPixel(x1, y0, mipLevel);
        Color c01 = GetPixel(x0, y1, mipLevel);
        Color c11 = GetPixel(x1, y1, mipLevel);

        Color c0 = Color.Lerp(c00, c10, tx);
        Color c1 = Color.Lerp(c01, c11, tx);
        return Color.Lerp(c0, c1, ty);
    }

    public void SetPixel(int x, int y, Color color)
        => SetPixel(x, y, color, 0);

    public void SetPixel(int x, int y, Color color, [Internal.DefaultValue("0")] int mipLevel)
    {
        if (!_isReadable) return;
        if (!TryGetMip(mipLevel, out Color[] pixels, out Color32[] pixels32,
                out int mipWidth, out int mipHeight) ||
            x < 0 || y < 0 || x >= mipWidth || y >= mipHeight)
        {
            return;
        }
        int index = y * mipWidth + x;
        pixels[index] = color;
        pixels32[index] = color;
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
        if (!_isReadable || !TryGetMip(miplevel, out Color[] pixels, out _, out _, out _))
            return Array.Empty<Color>();
        return (Color[])pixels.Clone();
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
        if (!_isReadable) return;
        if (colors == null) throw new ArgumentNullException(nameof(colors));
        if (!TryGetMip(miplevel, out Color[] pixels, out Color32[] pixels32, out _, out _))
            return;
        int count = Math.Min(colors.Length, pixels.Length);
        Array.Copy(colors, pixels, count);
        for (int index = 0; index < count; index++) pixels32[index] = colors[index];
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
        if (!_isReadable) return;
        if (colors == null) throw new ArgumentNullException(nameof(colors));
        if (!TryGetMip(miplevel, out Color[] pixels, out Color32[] pixels32, out _, out _))
            return;
        int count = Math.Min(colors.Length, pixels32.Length);
        Array.Copy(colors, pixels32, count);
        for (int index = 0; index < count; index++) pixels[index] = colors[index];
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
        if (!_isReadable || !TryGetMip(miplevel, out _, out Color32[] pixels, out _, out _))
            return Array.Empty<Color32>();
        return (Color32[])pixels.Clone();
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
        _mipmapsDirty = updateMipmaps;
        if (updateMipmaps && mipmapCount > 1) GenerateMipChain();
        IncrementUpdateCount();
        Anity.Core.Runtime.Native.NativeGraphicsDevice.Current?.EnsureTexture(this);
        if (makeNoLongerReadable)
        {
            _isReadable = false;
        }
    }

    internal byte[] GetNativeRgba32()
    {
        int pixelCount = 0;
        for (int mip = 0; mip < _mipPixels32.Length; mip++)
            pixelCount = checked(pixelCount + _mipPixels32[mip].Length);
        var result = new byte[checked(pixelCount * 4)];
        int output = 0;
        for (int mip = 0; mip < _mipPixels32.Length; mip++)
        {
            Color32[] pixels = _mipPixels32[mip];
            for (int index = 0; index < pixels.Length; index++)
            {
                Color32 pixel = pixels[index];
                result[output++] = pixel.r;
                result[output++] = pixel.g;
                result[output++] = pixel.b;
                result[output++] = pixel.a;
            }
        }
        return result;
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
        mipmapCount = hasMipMap ? GenerateMipsCount(width, height) : 1;
        InitializeMipStorage();
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
        if (markNonReadable)
        {
            _isReadable = false;
        }
        return true;
    }

    public void LoadRawTextureData(byte[] data)
    {
        if (data == null) return;
        int input = 0;
        for (int mip = 0; mip < _mipPixels.Length && input + 3 < data.Length; mip++)
        {
            for (int index = 0; index < _mipPixels[mip].Length && input + 3 < data.Length; index++)
            {
                var pixel = new Color32(data[input], data[input + 1], data[input + 2], data[input + 3]);
                _mipPixels32[mip][index] = pixel;
                _mipPixels[mip][index] = pixel;
                input += 4;
            }
        }
    }

    public byte[] GetRawTextureData()
    {
        return GetNativeRgba32();
    }

    internal int GetMipWidth(int mipLevel) => Math.Max(1, width >> mipLevel);
    internal int GetMipHeight(int mipLevel) => Math.Max(1, height >> mipLevel);

    private void InitializeMipStorage()
    {
        mipmapCount = Math.Max(1, Math.Min(mipmapCount, GenerateMipsCount(width, height)));
        _mipPixels = new Color[mipmapCount][];
        _mipPixels32 = new Color32[mipmapCount][];
        for (int mip = 0; mip < mipmapCount; mip++)
        {
            int pixelCount = checked(GetMipWidth(mip) * GetMipHeight(mip));
            _mipPixels[mip] = new Color[pixelCount];
            _mipPixels32[mip] = new Color32[pixelCount];
        }
        _pixels = _mipPixels[0];
        _pixels32 = _mipPixels32[0];
    }

    private bool TryGetMip(int mipLevel, out Color[] pixels, out Color32[] pixels32,
        out int mipWidth, out int mipHeight)
    {
        if (mipLevel < 0 || mipLevel >= _mipPixels.Length)
        {
            pixels = Array.Empty<Color>();
            pixels32 = Array.Empty<Color32>();
            mipWidth = 0;
            mipHeight = 0;
            return false;
        }
        pixels = _mipPixels[mipLevel];
        pixels32 = _mipPixels32[mipLevel];
        mipWidth = GetMipWidth(mipLevel);
        mipHeight = GetMipHeight(mipLevel);
        return true;
    }

    private void GenerateMipChain()
    {
        for (int mip = 1; mip < mipmapCount; mip++)
        {
            Color[] source = _mipPixels[mip - 1];
            Color[] destination = _mipPixels[mip];
            Color32[] destination32 = _mipPixels32[mip];
            int sourceWidth = GetMipWidth(mip - 1);
            int sourceHeight = GetMipHeight(mip - 1);
            int destinationWidth = GetMipWidth(mip);
            int destinationHeight = GetMipHeight(mip);
            for (int y = 0; y < destinationHeight; y++)
            {
                for (int x = 0; x < destinationWidth; x++)
                {
                    int x0 = Math.Min(sourceWidth - 1, x * 2);
                    int x1 = Math.Min(sourceWidth - 1, x0 + 1);
                    int y0 = Math.Min(sourceHeight - 1, y * 2);
                    int y1 = Math.Min(sourceHeight - 1, y0 + 1);
                    Color c00 = source[y0 * sourceWidth + x0];
                    Color c10 = source[y0 * sourceWidth + x1];
                    Color c01 = source[y1 * sourceWidth + x0];
                    Color c11 = source[y1 * sourceWidth + x1];
                    Color value = new(
                        (c00.r + c10.r + c01.r + c11.r) * 0.25f,
                        (c00.g + c10.g + c01.g + c11.g) * 0.25f,
                        (c00.b + c10.b + c01.b + c11.b) * 0.25f,
                        (c00.a + c10.a + c01.a + c11.a) * 0.25f);
                    int index = y * destinationWidth + x;
                    destination[index] = value;
                    destination32[index] = value;
                }
            }
        }
    }

    public T[] GetRawTextureData<T>() where T : struct
    {
        byte[] raw = GetRawTextureData();
        int elementSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
        if (elementSize <= 0) return Array.Empty<T>();
        int count = raw.Length / elementSize;
        var result = new T[count];
        Buffer.BlockCopy(raw, 0, result, 0, count * elementSize);
        return result;
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
        w.Write(ToBigEndian((uint)(_pixels.Length * 4 + height)));
        w.Write((byte)'I'); w.Write((byte)'D'); w.Write((byte)'A'); w.Write((byte)'T');
        var rawData = GetRawTextureData();
        for (int y = 0; y < height; y++)
        {
            w.Write((byte)0);
            w.Write(rawData, y * width * 4, width * 4);
        }
        w.Write(ToBigEndian(Crc32(ms.ToArray(), ms.Position - (rawData.Length + height) - 4, rawData.Length + height + 4)));
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

    public Rect[] PackTextures(Texture2D[] textures, int padding)
    {
        return PackTextures(textures, padding, 2048, false);
    }

    public Rect[] PackTextures(Texture2D[] textures, int padding, int maximumAtlasSize)
    {
        return PackTextures(textures, padding, maximumAtlasSize, false);
    }

    public Rect[] PackTextures(Texture2D[] textures, int padding, int maximumAtlasSize, bool makeNoLongerReadable)
    {
        if (textures == null || textures.Length == 0) return Array.Empty<Rect>();

        int totalArea = 0;
        int maxW = 0, maxH = 0;
        foreach (var t in textures)
        {
            if (t == null) continue;
            totalArea += (t.width + padding) * (t.height + padding);
            if (t.width > maxW) maxW = t.width;
            if (t.height > maxH) maxH = t.height;
        }

        int atlasSize = Math.Max(Math.Max(maxW + padding * 2, maxH + padding * 2), 32);
        while (atlasSize * atlasSize < totalArea * 2 && atlasSize < maximumAtlasSize)
            atlasSize *= 2;
        atlasSize = Math.Min(atlasSize, maximumAtlasSize);

        Resize(atlasSize, atlasSize, TextureFormat.RGBA32, false);

        var result = new Rect[textures.Length];
        int x = padding, y = padding, rowHeight = 0;

        for (int i = 0; i < textures.Length; i++)
        {
            var t = textures[i];
            if (t == null)
            {
                result[i] = new Rect(0, 0, 0, 0);
                continue;
            }

            int tw = t.width + padding;
            int th = t.height + padding;

            if (x + tw > atlasSize - padding)
            {
                x = padding;
                y += rowHeight;
                rowHeight = 0;
            }

            if (y + th > atlasSize - padding)
            {
                atlasSize = Math.Min(atlasSize * 2, maximumAtlasSize);
                Resize(atlasSize, atlasSize, TextureFormat.RGBA32, false);
                x = padding;
                y = padding;
                rowHeight = 0;
            }

            var srcPixels = t.GetPixels();
            for (int py = 0; py < t.height; py++)
            {
                for (int px = 0; px < t.width; px++)
                {
                    SetPixel(x + px, y + py, srcPixels[py * t.width + px]);
                }
            }

            result[i] = new Rect(
                (float)x / atlasSize,
                (float)y / atlasSize,
                (float)t.width / atlasSize,
                (float)t.height / atlasSize
            );

            x += tw;
            if (th > rowHeight) rowHeight = th;
        }

        Apply(false, makeNoLongerReadable);
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

    private static int _copyCount;
    public static void CopyTexture(Texture src, Texture dst) { _copyCount++; }
    public static void CopyTexture(Texture src, int srcElement, int srcMip, Texture dst, int dstElement, int dstMip) { _copyCount++; }
    public static void CopyTexture(Texture src, int srcElement, int srcMip, int srcX, int srcY, int srcWidth, int srcHeight, Texture dst, int dstElement, int dstMip, int dstX, int dstY) { _copyCount++; }
}
