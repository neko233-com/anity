using System;
using System.Collections.Generic;

namespace UnityEngine;

public enum RenderTextureFormat
{
    ARGB32 = 0,
    Depth = 1,
    ARGBHalf = 2,
    Shadowmap = 3,
    RGB565 = 4,
    ARGB4444 = 5,
    ARGB1555 = 6,
    Default = 7,
    ARGB2101010 = 8,
    DefaultHDR = 9,
    ARGBFloat = 11,
    RGFloat = 12,
    RGHalf = 13,
    RFloat = 14,
    RHalf = 15,
    R8 = 16,
    ARGBInt = 17,
    RGInt = 18,
    RInt = 19,
    BGRA32 = 20,
    RGB111110Float = 22,
    RG32 = 23,
    RGBAUShort = 24,
    RG16 = 25,
    BGRA10101010_XR = 26,
    BGR101010_XR = 27,
    R16 = 28
}

public enum RenderTextureReadWrite
{
    Default = 0,
    Linear = 1,
    sRGB = 2
}

public enum RenderTextureMemoryless
{
    None = 0,
    Color = 1,
    Depth = 2,
    MSAA = 4
}

public enum VRTextureUsage
{
    None = 0,
    OneEye = 1,
    TwoEyes = 2,
    DeviceSpecific = 3
}

public struct RenderTextureDescriptor
{
    public int width { get; set; }
    public int height { get; set; }
    public int volumeDepth { get; set; }
    public int msaaSamples { get; set; }
    public RenderTextureFormat colorFormat { get; set; }
    public int depthBufferBits { get; set; }
    public TextureDimension dimension { get; set; }
    public bool sRGB { get; set; }
    public bool useMipMap { get; set; }
    public bool autoGenerateMips { get; set; }
    public bool enableRandomWrite { get; set; }
    public RenderTextureMemoryless memoryless { get; set; }
    public VRTextureUsage vrUsage { get; set; }
    public int bindMS { get; set; }

    public RenderTextureDescriptor(int width, int height) : this(width, height, RenderTextureFormat.Default, 0) { }
    public RenderTextureDescriptor(int width, int height, RenderTextureFormat colorFormat) : this(width, height, colorFormat, 0) { }
    public RenderTextureDescriptor(int width, int height, RenderTextureFormat colorFormat, int depthBufferBits)
    {
        this.width = width;
        this.height = height;
        volumeDepth = 1;
        msaaSamples = 1;
        this.colorFormat = colorFormat;
        this.depthBufferBits = depthBufferBits;
        dimension = TextureDimension.Tex2D;
        sRGB = true;
        useMipMap = false;
        autoGenerateMips = true;
        enableRandomWrite = false;
        memoryless = RenderTextureMemoryless.None;
        vrUsage = VRTextureUsage.None;
        bindMS = 0;
    }
}

public class RenderTexture : Texture
{
    private bool _isCreated;
    private bool _contentsDiscarded;
    private bool _mipsDirty;
    private bool _restoreExpected;
    private static readonly Stack<RenderTexture> _activeTemporary = new();

    public new int width { get; set; }
    public new int height { get; set; }
    public int depth { get; }
    public int antiAliasing { get; set; }
    public RenderTextureFormat format { get; set; }
    public bool useMipMap { get; set; }
    public bool autoGenerateMips { get; set; }
    public bool enableRandomWrite { get; set; }
    public int volumeDepth { get; set; }
    public new TextureDimension dimension { get; set; }
    public RenderTextureMemoryless memorylessMode { get; set; }
    public VRTextureUsage vrUsage { get; set; }
    public int msaaSamples { get; set; }
    public new float mipMapBias { get; set; }
    public new int anisoLevel { get; set; }
    public RenderTextureReadWrite sRGB { get; set; }
    public bool bindTextureMS { get; set; }
    public bool isPowerOfTwo { get; set; }
    public RenderBuffer colorBuffer { get; }
    public RenderBuffer depthBuffer { get; }
    public IntPtr GetNativeDepthBufferPtr() => IntPtr.Zero;

    public static RenderTexture active
    {
        get => _activeTemporary.Count > 0 ? _activeTemporary.Peek() : null;
        set
        {
            if (value != null)
                _activeTemporary.Push(value);
        }
    }

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
        base.width = width;
        base.height = height;
        base.dimension = dimension;
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
        base.width = width;
        base.height = height;
        base.dimension = dimension;
    }

    public bool IsCreated() => _isCreated;

    public void Create()
    {
        _isCreated = true;
    }

    public void Release()
    {
        _isCreated = false;
    }

    public void DiscardContents() { _contentsDiscarded = true; }
    public void DiscardContents(bool discardColor, bool discardDepth) { _contentsDiscarded = discardColor || discardDepth; }

    public static bool SupportsStencil(RenderTexture rt) => rt != null;

    public new IntPtr GetNativeTexturePtr() => IntPtr.Zero;
    public IntPtr GetDepthStencilNativeTexturePtr() => IntPtr.Zero;

    public void GenerateMips() { _mipsDirty = true; }
    public void SetGlobalShaderProperty(string propertyName) { _ = propertyName; }

    public void MarkRestoreExpected() { _restoreExpected = true; }

    public static RenderTexture GetTemporary(RenderTextureDescriptor desc)
    {
        return new RenderTexture(desc);
    }

    public static RenderTexture GetTemporary(int width, int height)
    {
        return GetTemporary(width, height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default, 1);
    }

    public static RenderTexture GetTemporary(int width, int height, int depthBuffer)
    {
        return GetTemporary(width, height, depthBuffer, RenderTextureFormat.Default, RenderTextureReadWrite.Default, 1);
    }

    public static RenderTexture GetTemporary(int width, int height, int depthBuffer, RenderTextureFormat format)
    {
        return GetTemporary(width, height, depthBuffer, format, RenderTextureReadWrite.Default, 1);
    }

    public static RenderTexture GetTemporary(int width, int height, int depthBuffer, RenderTextureFormat format, RenderTextureReadWrite readWrite)
    {
        return GetTemporary(width, height, depthBuffer, format, readWrite, 1);
    }

    public static RenderTexture GetTemporary(int width, int height, int depthBuffer, RenderTextureFormat format, RenderTextureReadWrite readWrite, int antiAliasing)
    {
        var rt = new RenderTexture(width, height, depthBuffer, format, readWrite);
        rt.antiAliasing = antiAliasing;
        rt.Create();
        return rt;
    }

    public static void ReleaseTemporary(RenderTexture temp)
    {
        temp?.Release();
    }

    public void ReleaseTemporary()
    {
        Release();
    }
}

public struct RenderBuffer
{
    public RenderTextureFormat format { get; set; }
    private bool _debugModeLoaded;
    public IntPtr GetNativeRenderBufferPtr() => IntPtr.Zero;
    public void LoadStoreActionDebugModeSettings() { _debugModeLoaded = true; }
}
