using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

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

public enum ShadowSamplingMode
{
    CompareDepths = 0,
    StencilDepths = 1,
    SoftShadows = 2
}

public enum CustomRenderTextureUpdateMode
{
    OnLoad = 0,
    Realtime = 1,
    OnDemand = 2
}

public enum CustomRenderTextureInitializationSource
{
    TextureAndColor = 0,
    Material = 1
}

public enum CustomRenderTextureUpdateZoneSpace
{
    Normalized = 0,
    Pixel = 1
}

public struct RenderTextureDescriptor
{
    public int width { get; set; }
    public int height { get; set; }
    public int volumeDepth { get; set; }
    public int msaaSamples { get; set; }
    public RenderTextureFormat colorFormat { get; set; }
    public GraphicsFormat graphicsFormat { get; set; }
    public int depthBufferBits { get; set; }
    public int mipCount { get; set; }
    public TextureDimension dimension { get; set; }
    public bool sRGB { get; set; }
    public bool useMipMap { get; set; }
    public bool autoGenerateMips { get; set; }
    public bool enableRandomWrite { get; set; }
    public RenderTextureMemoryless memoryless { get; set; }
    public VRTextureUsage vrUsage { get; set; }
    public int bindMS { get; set; }
    public ShadowSamplingMode shadowSamplingMode { get; set; }

    public RenderTextureDescriptor(int width, int height) : this(width, height, RenderTextureFormat.Default, 0) { }
    public RenderTextureDescriptor(int width, int height, RenderTextureFormat colorFormat) : this(width, height, colorFormat, 0) { }
    public RenderTextureDescriptor(int width, int height, RenderTextureFormat colorFormat, int depthBufferBits) : this(width, height, colorFormat, depthBufferBits, 0) { }
    public RenderTextureDescriptor(int width, int height, RenderTextureFormat colorFormat, int depthBufferBits, int mipCount)
    {
        this.width = width;
        this.height = height;
        volumeDepth = 1;
        msaaSamples = 1;
        this.colorFormat = colorFormat;
        graphicsFormat = GraphicsFormat.None;
        this.depthBufferBits = depthBufferBits;
        this.mipCount = mipCount;
        dimension = TextureDimension.Tex2D;
        sRGB = true;
        useMipMap = mipCount > 0;
        autoGenerateMips = true;
        enableRandomWrite = false;
        memoryless = RenderTextureMemoryless.None;
        vrUsage = VRTextureUsage.None;
        bindMS = 0;
        shadowSamplingMode = ShadowSamplingMode.CompareDepths;
    }
}

public class RenderTexture : Texture
{
    private bool _isCreated;
    private bool _contentsDiscarded;
    private bool _mipsDirty;
    private bool _restoreExpected;
    private static readonly Stack<RenderTexture> _activeTemporary = new();
    private RenderTextureDescriptor _descriptor;

    public new int width { get; set; }
    public new int height { get; set; }
    public int depth { get; protected set; }
    public int antiAliasing { get; set; }
    public RenderTextureFormat format { get; set; }
    public GraphicsFormat graphicsFormat { get; set; }
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
    public bool doubleBuffered { get; set; }
    public ShadowSamplingMode shadowSamplingMode { get; set; }
    public RenderBuffer colorBuffer { get; }
    public RenderBuffer depthBuffer { get; }

    public RenderTextureDescriptor descriptor
    {
        get => _descriptor;
        set
        {
            _descriptor = value;
            width = value.width;
            height = value.height;
            depth = value.depthBufferBits;
            volumeDepth = value.volumeDepth;
            msaaSamples = value.msaaSamples;
            antiAliasing = value.msaaSamples;
            format = value.colorFormat;
            graphicsFormat = value.graphicsFormat;
            dimension = value.dimension;
            useMipMap = value.useMipMap;
            autoGenerateMips = value.autoGenerateMips;
            enableRandomWrite = value.enableRandomWrite;
            memorylessMode = value.memoryless;
            vrUsage = value.vrUsage;
            bindTextureMS = value.bindMS > 0;
            shadowSamplingMode = value.shadowSamplingMode;
            sRGB = value.sRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear;
            base.width = width;
            base.height = height;
            base.dimension = dimension;
        }
    }

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
        var desc = new RenderTextureDescriptor(width, height, format, depth);
        descriptor = desc;
        filterMode = FilterMode.Point;
        wrapMode = TextureWrapMode.Repeat;
    }

    public RenderTexture(int width, int height, int depth, RenderTextureFormat format, RenderTextureReadWrite readWrite)
      : this(width, height, depth, format)
    {
        sRGB = readWrite;
    }

    public RenderTexture(RenderTextureDescriptor desc)
    {
        descriptor = desc;
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

public class CustomRenderTexture : RenderTexture
{
    private bool _doubleBuffered;
    private CustomRenderTextureUpdateMode _updateMode = CustomRenderTextureUpdateMode.OnLoad;
    private CustomRenderTextureInitializationSource _initializationSource = CustomRenderTextureInitializationSource.TextureAndColor;
    private CustomRenderTextureUpdateZoneSpace _updateZoneSpace = CustomRenderTextureUpdateZoneSpace.Normalized;
    private Texture _initializationTexture;
    private Material _initializationMaterial;
    private Material _updateMaterial;
    private Color _initializationColor = Color.clear;

    public new bool doubleBuffered
    {
        get => _doubleBuffered;
        set { _doubleBuffered = value; base.doubleBuffered = value; }
    }

    public CustomRenderTextureUpdateMode updateMode
    {
        get => _updateMode;
        set => _updateMode = value;
    }

    public CustomRenderTextureInitializationSource initializationSource
    {
        get => _initializationSource;
        set => _initializationSource = value;
    }

    public CustomRenderTextureUpdateZoneSpace updateZoneSpace
    {
        get => _updateZoneSpace;
        set => _updateZoneSpace = value;
    }

    public Texture initializationTexture
    {
        get => _initializationTexture;
        set => _initializationTexture = value;
    }

    public Material initializationMaterial
    {
        get => _initializationMaterial;
        set => _initializationMaterial = value;
    }

    public Material updateMaterial
    {
        get => _updateMaterial;
        set => _updateMaterial = value;
    }

    public Color initializationColor
    {
        get => _initializationColor;
        set => _initializationColor = value;
    }

    public CustomRenderTexture(int width, int height) : base(width, height, 0) { }
    public CustomRenderTexture(int width, int height, RenderTextureFormat format) : base(width, height, 0, format) { }
    public CustomRenderTexture(int width, int height, RenderTextureFormat format, RenderTextureReadWrite readWrite) : base(width, height, 0, format, readWrite) { }

    public void Initialize() { }
    public void Update(int count = 1) { }
    public void ClearUpdateZones() { }
}

public struct RenderBuffer
{
    public RenderTextureFormat format { get; set; }
    public RenderBufferLoadAction loadAction { get; set; }
    public RenderBufferStoreAction storeAction { get; set; }
    private bool _debugModeLoaded;
    public IntPtr GetNativeRenderBufferPtr() => IntPtr.Zero;
    public void LoadStoreActionDebugModeSettings() { _debugModeLoaded = true; }
}

public struct RenderTargetSetup
{
    public RenderBuffer[] color { get; set; }
    public RenderBuffer depth { get; set; }
    public int mipLevel { get; set; }
    public CubemapFace cubemapFace { get; set; }
    public int depthSlice { get; set; }
    public RenderBufferLoadAction[] colorLoad { get; set; }
    public RenderBufferStoreAction[] colorStore { get; set; }
    public RenderBufferLoadAction depthLoad { get; set; }
    public RenderBufferStoreAction depthStore { get; set; }

    public RenderTargetSetup(RenderBuffer color, RenderBuffer depth)
    {
        this.color = new[] { color };
        this.depth = depth;
        mipLevel = 0;
        cubemapFace = CubemapFace.Unknown;
        depthSlice = 0;
        colorLoad = null;
        colorStore = null;
        depthLoad = RenderBufferLoadAction.Load;
        depthStore = RenderBufferStoreAction.Store;
    }
}
