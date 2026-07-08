namespace UnityEngine;

public enum RenderTextureFormat
{
  ARGB32 = 0,
  Depth = 1,
  Shadow = 2,
  Alpha8 = 3,
  RGB24 = 4,
  RGB565 = 5,
  ARGB1555 = 6,
  Default = 7,
  ARGB2101010 = 8,
  ARGBHalf = 9,
  RGFloat = 10,
  RGHalf = 11,
  RFloat = 12,
  RHalf = 13,
  R8 = 14,
  RGInt = 15,
  RInt = 16,
  BGRA32 = 17,
  RGB111110Float = 22,
  RG32 = 23,
  RGBAUShort = 24,
  RG16 = 25,
  BGRA10101010_XR = 26,
  BGR101010_XR = 27,
  R16 = 28,
}

public class RenderTexture : Texture
{
  public int width { get; set; }
  public int height { get; set; }
  public int depth { get; }
  public int antiAliasing { get; set; }
  public RenderTextureFormat format { get; set; }
  public bool useMipMap { get; set; }
  public bool autoGenerateMips { get; set; }
  public FilterMode filterMode { get; set; }
  public TextureWrapMode wrapMode { get; set; }

  public static RenderTexture active { get; set; }

  public RenderTexture(int width, int height, int depth)
    : this(width, height, depth, RenderTextureFormat.ARGB32)
  {
  }

  public RenderTexture(int width, int height, int depth, RenderTextureFormat format)
  {
    this.width = width;
    this.height = height;
    this.depth = depth;
    this.format = format;
    filterMode = FilterMode.Point;
    wrapMode = TextureWrapMode.Repeat;
  }
}

public class Texture : Object
{
  public virtual void Release() {}
  public virtual bool IsCreated() => true;
  public virtual void Create() {}
}
