using System;

namespace UnityEngine;

public class RenderTexture : Texture
{
  public new int width { get; set; }
  public new int height { get; set; }
  public int depth { get; }
  public int antiAliasing { get; set; }
  public RenderTextureFormat format { get; set; }
  public bool useMipMap { get; set; }
  public bool autoGenerateMips { get; set; }
  public bool enableRandomWrite { get; set; }
  public int volumeDepth { get; set; }
  public TextureDimension dimension { get; set; }
  public RenderTextureMemoryless memorylessMode { get; set; }
  public VRTextureUsage vrUsage { get; set; }
  public int msaaSamples { get; set; }
  public float mipMapBias { get; set; }
  public int anisoLevel { get; set; }
  public RenderTextureReadWrite sRGB { get; set; }

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
    antiAliasing = 1;
    msaaSamples = 1;
    volumeDepth = 1;
    dimension = TextureDimension.Tex2D;
    sRGB = RenderTextureReadWrite.Default;
    memorylessMode = RenderTextureMemoryless.None;
    vrUsage = VRTextureUsage.None;
  }

  public RenderTexture(int width, int height, int depth, RenderTextureFormat format, RenderTextureReadWrite readWrite)
    : this(width, height, depth, format)
  {
    sRGB = readWrite;
  }

  public RenderTexture(RenderTextureDescriptor desc)
  {
    width = desc.width;
    height = desc.height;
    depth = desc.depthBufferBits;
    format = desc.colorFormat;
    antiAliasing = desc.msaaSamples;
    msaaSamples = desc.msaaSamples;
    volumeDepth = desc.volumeDepth;
    dimension = desc.dimension;
    useMipMap = desc.useMipMap;
    autoGenerateMips = desc.autoGenerateMips;
    enableRandomWrite = desc.enableRandomWrite;
    memorylessMode = desc.memoryless;
    vrUsage = desc.vrUsage;
    if (desc.sRGB)
      sRGB = RenderTextureReadWrite.sRGB;
    else
      sRGB = RenderTextureReadWrite.Linear;
  }

  public bool IsCreated() => true;

  public void Create()
  {
  }

  public void Release()
  {
  }

  public void DiscardContents()
  {
  }

  public void DiscardContents(bool discardColor, bool discardDepth)
  {
    _ = discardColor;
    _ = discardDepth;
  }

  public static bool SupportsStencil(RenderTexture rt)
  {
    return rt != null;
  }

  public IntPtr GetNativeTexturePtr()
  {
    return IntPtr.Zero;
  }

  public IntPtr GetDepthStencilNativeTexturePtr()
  {
    return IntPtr.Zero;
  }

  public void GenerateMips()
  {
  }

  public void SetGlobalShaderProperty(string propertyName)
  {
  }
}