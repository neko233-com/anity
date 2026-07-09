namespace UnityEngine;

public class Cubemap : Texture
{
  public TextureFormat format { get; set; }
  public bool mipChain { get; set; } = true;
  public bool linear { get; set; }
  public bool requestedMipmapLevel { get; set; }
  public bool desiredMipmapLevel { get; set; }
  public int mipMapBias { get; set; }

  public Cubemap(int width, TextureFormat format, bool mipChain)
  {
    this.width = width;
    this.height = width;
    this.format = format;
    this.mipChain = mipChain;
    dimension = TextureDimension.Cube;
  }

  public Cubemap(int width, TextureFormat format, bool mipChain, bool linear)
    : this(width, format, mipChain)
  {
    this.linear = linear;
  }

  public Color GetPixel(CubemapFace face, int x, int y)
  {
    _ = face;
    _ = x;
    _ = y;
    return Color.clear;
  }

  public void SetPixel(CubemapFace face, int x, int y, Color color)
  {
    _ = face;
    _ = x;
    _ = y;
    _ = color;
  }

  public Color[] GetPixels(CubemapFace face)
  {
    _ = face;
    return new Color[0];
  }

  public void SetPixels(CubemapFace face, Color[] colors)
  {
    _ = face;
    _ = colors;
  }

  public Color[] GetPixels(CubemapFace face, int miplevel)
  {
    _ = face;
    _ = miplevel;
    return new Color[0];
  }

  public void SetPixels(CubemapFace face, int miplevel, Color[] colors)
  {
    _ = face;
    _ = miplevel;
    _ = colors;
  }

  public void Apply()
  {
  }

  public void Apply(bool updateMipmaps)
  {
    _ = updateMipmaps;
  }

  public void Apply(bool updateMipmaps, bool makeNoLongerReadable)
  {
    _ = updateMipmaps;
    _ = makeNoLongerReadable;
  }

  public void SmoothEdges()
  {
  }

  public void SmoothEdges(int mipLevel)
  {
    _ = mipLevel;
  }
}
