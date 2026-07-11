using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnityEngine;

public class Cubemap : Texture
{
    private Dictionary<CubemapFace, Color[]> _facePixels = new();
    private bool _mipmapsDirty;
    private bool _isReadable = true;
    public TextureFormat format { get; set; }
    public bool mipChain { get; set; } = true;
    public bool linear { get; set; }
    public int requestedMipmapLevel { get; set; }
    public int desiredMipmapLevel { get; set; }
    public int mipMapBias { get; set; }

    public Cubemap(int width, TextureFormat format, bool mipChain)
    {
        this.width = width;
        this.height = width;
        this.format = format;
        this.mipChain = mipChain;
        dimension = TextureDimension.Cube;
        int pixelCount = Math.Max(1, width * width);
        for (int f = 0; f < 6; f++)
            _facePixels[(CubemapFace)f] = new Color[pixelCount];
    }

    public Cubemap(int width, TextureFormat format, bool mipChain, bool linear)
        : this(width, format, mipChain)
    {
        this.linear = linear;
    }

    public override bool isReadable => _isReadable;

    private int GetPixelIndex(int x, int y) => y * width + x;

    public Color GetPixel(CubemapFace face, int x, int y)
    {
        if (!_isReadable) return Color.clear;
        if (x < 0 || y < 0 || x >= width || y >= height || !_facePixels.ContainsKey(face))
            return Color.clear;
        return _facePixels[face][GetPixelIndex(x, y)];
    }

    public void SetPixel(CubemapFace face, int x, int y, Color color)
    {
        if (!_isReadable) return;
        if (x < 0 || y < 0 || x >= width || y >= height || !_facePixels.ContainsKey(face))
            return;
        _facePixels[face][GetPixelIndex(x, y)] = color;
    }

    public Color[] GetPixels(CubemapFace face)
    {
        if (!_isReadable) return Array.Empty<Color>();
        if (_facePixels.TryGetValue(face, out var pixels))
        {
            var clone = new Color[pixels.Length];
            Array.Copy(pixels, clone, pixels.Length);
            return clone;
        }
        return Array.Empty<Color>();
    }

    public void SetPixels(CubemapFace face, Color[] colors)
    {
        if (!_isReadable) return;
        if (colors == null || !_facePixels.ContainsKey(face)) return;
        int count = Math.Min(colors.Length, _facePixels[face].Length);
        Array.Copy(colors, _facePixels[face], count);
    }

    public Color[] GetPixels(CubemapFace face, int miplevel) => GetPixels(face);

    public void SetPixels(CubemapFace face, int miplevel, Color[] colors) => SetPixels(face, colors);

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

    public void SmoothEdges()
    {
        for (int f = 0; f < 6; f++)
            SmoothEdge((CubemapFace)f);
    }

    public void SmoothEdges(int mipLevel) => SmoothEdges();

    private void SmoothEdge(CubemapFace face)
    {
        var pixels = _facePixels[face];
        if (pixels == null || width < 2) return;
        for (int y = 0; y < height; y++)
        {
            pixels[GetPixelIndex(0, y)] = Color.Lerp(pixels[GetPixelIndex(0, y)], pixels[GetPixelIndex(width - 1, y)], 0.5f);
            pixels[GetPixelIndex(width - 1, y)] = pixels[GetPixelIndex(0, y)];
        }
    }
}
