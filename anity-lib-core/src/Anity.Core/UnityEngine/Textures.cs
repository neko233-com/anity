using System;

namespace UnityEngine;

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

public class Texture3D : Texture
{
    private Color[] _pixels;
    private bool _mipmapsDirty;
    private bool _isReadable = true;
    public int depth { get; private set; }
    public TextureFormat format { get; set; }

    public Texture3D(int width, int height, int depth, TextureFormat format, bool mipmap)
    {
        this.width = width;
        this.height = height;
        this.depth = depth;
        this.format = format;
        dimension = TextureDimension.Tex3D;
        int pixelCount = Math.Max(1, width * height * depth);
        _pixels = new Color[pixelCount];
    }

    public Texture3D(int width, int height, int depth, TextureFormat format, bool mipmap, bool linear)
        : this(width, height, depth, format, mipmap)
    {
        _ = linear;
    }

    public override bool isReadable => _isReadable;

    private int GetPixelIndex(int x, int y, int z) => z * width * height + y * width + x;

    public Color GetPixel(int x, int y, int z)
    {
        if (!_isReadable) return Color.clear;
        if (x < 0 || y < 0 || z < 0 || x >= width || y >= height || z >= depth)
            return Color.clear;
        return _pixels[GetPixelIndex(x, y, z)];
    }

    public void SetPixel(int x, int y, int z, Color color)
    {
        if (!_isReadable) return;
        if (x < 0 || y < 0 || z < 0 || x >= width || y >= height || z >= depth)
            return;
        _pixels[GetPixelIndex(x, y, z)] = color;
    }

    public Color[] GetPixels()
    {
        if (!_isReadable) return Array.Empty<Color>();
        var clone = new Color[_pixels.Length];
        Array.Copy(_pixels, clone, _pixels.Length);
        return clone;
    }

    public Color[] GetPixels(int mipLevel)
    {
        return GetPixels();
    }

    public void SetPixels(Color[] colors)
    {
        if (!_isReadable) return;
        if (colors == null) throw new ArgumentNullException(nameof(colors));
        int count = Math.Min(colors.Length, _pixels.Length);
        Array.Copy(colors, _pixels, count);
    }

    public void SetPixels(Color[] colors, int mipLevel)
    {
        SetPixels(colors);
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
        _mipmapsDirty = true;
        if (makeNoLongerReadable)
        {
            _isReadable = false;
        }
    }
}

public class Texture2DArray : Texture
{
    private Dictionary<int, Color[]> _pixelData = new();
    private bool _mipmapsDirty;
    private bool _isReadable = true;
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
        int pixelCount = Math.Max(1, width * height);
        for (int i = 0; i < depth; i++)
            _pixelData[i] = new Color[pixelCount];
    }

    public Texture2DArray(int width, int height, int depth, TextureFormat format, bool mipmap, bool linear)
        : this(width, height, depth, format, mipmap)
    {
        _ = linear;
    }

    public override bool isReadable => _isReadable;

    public Color[] GetPixels(int arrayElement, int miplevel = 0)
    {
        if (!_isReadable) return Array.Empty<Color>();
        if (_pixelData.TryGetValue(arrayElement, out var pixels))
        {
            var clone = new Color[pixels.Length];
            Array.Copy(pixels, clone, pixels.Length);
            return clone;
        }
        return Array.Empty<Color>();
    }

    public void SetPixels(Color[] pixels, int arrayElement, int miplevel = 0)
    {
        if (!_isReadable) return;
        if (pixels == null) return;
        if (!_pixelData.ContainsKey(arrayElement))
            _pixelData[arrayElement] = new Color[Math.Max(1, width * height)];
        int count = Math.Min(pixels.Length, _pixelData[arrayElement].Length);
        Array.Copy(pixels, _pixelData[arrayElement], count);
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
        _mipmapsDirty = true;
        if (makeNoLongerReadable)
        {
            _isReadable = false;
        }
    }
}

public class CubemapArray : Texture
{
    private Dictionary<(CubemapFace, int), Color[]> _pixelData = new();
    private bool _mipmapsDirty;
    private bool _isReadable = true;
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
        int pixelCount = Math.Max(1, faceSize * faceSize);
        for (int f = 0; f < 6; f++)
            for (int i = 0; i < cubemapCount; i++)
                _pixelData[((CubemapFace)f, i)] = new Color[pixelCount];
    }

    public CubemapArray(int faceSize, int cubemapCount, TextureFormat format, bool mipmap, bool linear)
        : this(faceSize, cubemapCount, format, mipmap)
    {
        _ = linear;
    }

    public override bool isReadable => _isReadable;

    public Color[] GetPixels(CubemapFace face, int arrayElement, int miplevel = 0)
    {
        if (!_isReadable) return Array.Empty<Color>();
        if (_pixelData.TryGetValue((face, arrayElement), out var pixels))
        {
            var clone = new Color[pixels.Length];
            Array.Copy(pixels, clone, pixels.Length);
            return clone;
        }
        return Array.Empty<Color>();
    }

    public void SetPixels(Color[] pixels, CubemapFace face, int arrayElement, int miplevel = 0)
    {
        if (!_isReadable) return;
        if (pixels == null) return;
        var key = (face, arrayElement);
        if (!_pixelData.ContainsKey(key))
            _pixelData[key] = new Color[Math.Max(1, width * height)];
        int count = Math.Min(pixels.Length, _pixelData[key].Length);
        Array.Copy(pixels, _pixelData[key], count);
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
        _mipmapsDirty = true;
        if (makeNoLongerReadable)
        {
            _isReadable = false;
        }
    }
}
