using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering;

public struct ScriptableCullingParameters
{
    public Camera camera;
    public Matrix4x4 cullingMatrix;
    public Vector3 worldOrigin;
    public bool cullStereoSeparate;
    public CullingOptions cullingOptions;
    public Vector3 lodParameters;
    public int cullingMask;
    public float shadowDistance;
    public bool conservative;
    public Vector4[] shadowCascadeDistances;
    public float[] layerCullDistances;
    public bool stereoProjectionMatrix;

    public ScriptableCullingParameters(Camera camera)
    {
        this.camera = camera;
        cullingMatrix = camera != null ? camera.projectionMatrix * camera.worldToCameraMatrix : Matrix4x4.identity;
        worldOrigin = camera != null && camera.transform != null ? camera.transform.position : Vector3.zero;
        cullStereoSeparate = false;
        cullingOptions = CullingOptions.None;
        lodParameters = new Vector3(0, 0, QualitySettings.lodBias);
        cullingMask = camera != null ? camera.cullingMask : -1;
        shadowDistance = QualitySettings.shadowDistance;
        conservative = false;
        shadowCascadeDistances = null;
        layerCullDistances = null;
        stereoProjectionMatrix = false;
    }

    public static bool GetCullingParameters(Camera camera, out ScriptableCullingParameters parameters)
    {
        parameters = new ScriptableCullingParameters(camera);
        return camera != null;
    }
}

public enum CullingOptions
{
    None = 0,
    ForceEvenIfCameraIsNotActive = 1,
    OcclusionCull = 2,
    NearestPortal = 4,
}

public static class GraphicsSettings
{
    private static RenderPipelineAsset? _defaultRenderPipeline;
    private static RenderPipeline? _currentPipelineInstance;
    private static readonly List<Action<RenderPipelineAsset, RenderPipelineAsset>> _renderPipelineChangedListeners = new();
    private static readonly Dictionary<Type, RenderPipelineAsset> _renderPipelineSettings = new();

    public static RenderPipelineAsset? currentRenderPipeline => QualitySettings.renderPipeline ?? _defaultRenderPipeline;

    public static RenderPipelineAsset? defaultRenderPipeline
    {
        get => _defaultRenderPipeline;
        set
        {
            if (_defaultRenderPipeline == value) return;
            var oldAsset = _defaultRenderPipeline;
            _defaultRenderPipeline = value;

            if (QualitySettings.renderPipeline == null)
                SwitchPipeline(oldAsset, value);
        }
    }

    public static RenderPipeline? currentRenderPipelineInstance => _currentPipelineInstance;
    public static Shader? defaultShader { get; set; }
    public static Material? defaultSpatialMaterial { get; set; }
    public static bool useScriptableRenderPipeline => currentRenderPipeline != null;
    public static bool useScriptableRenderPipelineBatching { get; set; }
    public static bool logWhenShaderIsCompiled { get; set; }
    public static bool disableBuiltinKeywordRenderPipeline { get; set; }
    public static bool disableBuiltinKeywordAmbientObscurance { get; set; }
    public static bool lightsUseColorTemperature { get; set; }
    public static bool lightsUseLinearIntensity { get; set; }
    public static TransparencySortMode transparencySortMode { get; set; } = TransparencySortMode.Default;
    public static Vector3 transparencySortAxis { get; set; } = Vector3.forward;
    public static bool realtimeDirectRectangularAreaLights { get; set; }
    public static bool realtimeIndirectRectangularAreaLights { get; set; }
    public static bool defaultRenderingLayerMask { get; set; } = true;

    public static event Action<RenderPipelineAsset, RenderPipelineAsset> renderPipelineChanged
    {
        add => _renderPipelineChangedListeners.Add(value);
        remove => _renderPipelineChangedListeners.Remove(value);
    }

    public static int shaderRenderPipelineAssetCount { get; }
    public static int customRenderPipelineAssetCount { get; }

    internal static void OnQualityPipelineChanged(RenderPipelineAsset? oldAsset, RenderPipelineAsset? newAsset)
    {
        SwitchPipeline(oldAsset, newAsset);
    }

    private static void SwitchPipeline(RenderPipelineAsset? oldAsset, RenderPipelineAsset? newAsset)
    {
        if (_currentPipelineInstance != null)
        {
            _currentPipelineInstance.Dispose();
            _currentPipelineInstance = null;
        }

        RenderPipelineManager.SetCurrentPipeline(null);

        if (newAsset != null)
        {
            _currentPipelineInstance = newAsset.InternalCreatePipeline();
            RenderPipelineManager.SetCurrentPipeline(_currentPipelineInstance);
        }

        foreach (var listener in _renderPipelineChangedListeners)
        {
            try { listener(oldAsset, newAsset); } catch { }
        }
    }

    public static void RegisterRenderPipelineSettings<T>(T settings) where T : RenderPipelineAsset
    {
        _renderPipelineSettings[typeof(T)] = settings;
    }

    public static void UnregisterRenderPipelineSettings<T>(T settings) where T : RenderPipelineAsset
    {
        _renderPipelineSettings.Remove(typeof(T));
    }

    public static T? GetRenderPipelineSettings<T>() where T : RenderPipelineAsset
    {
        if (_renderPipelineSettings.TryGetValue(typeof(T), out var settings))
            return settings as T;
        return null;
    }

    public static bool HasShaderDefine(GraphicsDeviceType graphicsDeviceType, BuiltinShaderDefine define)
    {
        _ = graphicsDeviceType;
        _ = define;
        return false;
    }

    internal static void UpdateCurrentPipeline()
    {
        var asset = currentRenderPipeline;
        SwitchPipeline(_defaultRenderPipeline, asset);
    }
}

public enum TransparencySortMode
{
    Default = 0,
    Perspective = 1,
    Orthographic = 2,
    CustomAxis = 3
}

public enum GraphicsDeviceType
{
    Direct3D11 = 2,
    Null = 4,
    OpenGLES2 = 8,
    OpenGLES3 = 11,
    PlayStation4 = 13,
    XboxOne = 14,
    Metal = 16,
    OpenGLCore = 17,
    Direct3D12 = 18,
    Vulkan = 21,
    Switch = 22,
    XboxOneD3D12 = 23,
    PlayStation5 = 26,
    WebGL2 = 28,
    WebGPU = 29
}

public enum BuiltinShaderDefine
{
    UNITY_NO_DXT5NM,
    UNITY_NO_RGBM,
    UNITY_USE_NATIVE_HDR,
    UNITY_ENABLE_REFLECTION_BUFFERS,
    UNITY_FRAMEBUFFER_FETCH_AVAILABLE,
    UNITY_ENABLE_NATIVE_SHADOW_LOOKUPS,
    UNITY_METAL_SHADOWS_USE_POINT_FILTERING,
    UNITY_NO_CUBEMAP_ARRAY,
    UNITY_NO_SCREENSPACE_SHADOWS
}

public static class SupportedRenderingFeatures
{
    public static ReflectionProbeSupportFlags reflectionProbeSupportFlags { get; set; }
    public static bool defaultMixedLightingMode { get; set; }
    public static bool mixedLightingModes { get; set; }
    public static bool lightProbeProxyVolumes { get; set; }
    public static bool motionVectors { get; set; }
    public static bool receiveShadows { get; set; }
    public static bool reflectionProbes { get; set; }
    public static bool rendererPriority { get; set; }
    public static bool overridesEnvironmentLighting { get; set; }
    public static bool overridesFog { get; set; }
    public static bool editableMaterialRenderQueue { get; set; }
}

public enum ReflectionProbeSupportFlags
{
    None = 0
}

public static class RenderPipelineGlobalSettings
{
    public static RenderPipelineGlobalSettingsSO? instance { get; set; }
}

public class RenderPipelineGlobalSettingsSO : ScriptableObject
{
}

[Flags]
public enum ClearFlag
{
    None = 0,
    Color = 1,
    Depth = 2,
    Stencil = 4,
    All = Color | Depth | Stencil,
}

public enum CameraEvent
{
    BeforeDepthTexture = 0,
    AfterDepthTexture = 1,
    BeforeGBuffer = 2,
    AfterGBuffer = 3,
    BeforeDeferredReflections = 4,
    AfterDeferredReflections = 5,
    BeforeMainLightShadowMap = 6,
    AfterMainLightShadowMap = 7,
    BeforeAdditionalLightShadowMap = 8,
    AfterAdditionalLightShadowMap = 9,
    BeforeScreenSpaceOcclusion = 10,
    AfterScreenSpaceOcclusion = 11,
    BeforeHaloAndLensFlares = 12,
    AfterHaloAndLensFlares = 13,
    BeforeImageEffects = 14,
    AfterImageEffects = 15,
    AfterEverything = 16,
    BeforeReflections = 17,
    AfterReflections = 18,
    BeforeLighting = 19,
    AfterLighting = 20,
    BeforeForwardOpaque = 21,
    AfterForwardOpaque = 22,
    BeforeForwardAlpha = 23,
    AfterForwardAlpha = 24,
    BeforeFinalBlit = 25,
    AfterFinalBlit = 26,
}

public enum LightEvent
{
    BeforeShadowMap = 0,
    AfterShadowMap = 1,
    BeforeShadowMapPass = 2,
    AfterShadowMapPass = 3,
}

public enum ShadowMapPass
{
    Default = 0,
    Point = 1,
    Directional = 2,
    Spot = 3,
}

public struct VolumePriority : IComparable<VolumePriority>, IEquatable<VolumePriority>
{
    private float _value;

    public VolumePriority(float value)
    {
        _value = value;
    }

    public int CompareTo(VolumePriority other)
    {
        return _value.CompareTo(other._value);
    }

    public bool Equals(VolumePriority other)
    {
        return _value == other._value;
    }

    public override bool Equals(object obj)
    {
        return obj is VolumePriority other && Equals(other);
    }

    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }

    public static implicit operator float(VolumePriority priority)
    {
        return priority._value;
    }

    public static implicit operator VolumePriority(float value)
    {
        return new VolumePriority(value);
    }

    public static bool operator ==(VolumePriority lhs, VolumePriority rhs)
    {
        return lhs._value == rhs._value;
    }

    public static bool operator !=(VolumePriority lhs, VolumePriority rhs)
    {
        return lhs._value != rhs._value;
    }

    public static bool operator >(VolumePriority lhs, VolumePriority rhs)
    {
        return lhs._value > rhs._value;
    }

    public static bool operator <(VolumePriority lhs, VolumePriority rhs)
    {
        return lhs._value < rhs._value;
    }

    public override string ToString()
    {
        return _value.ToString();
    }
}

public enum StencilUsage
{
    UserMaskRange = 0,
    SDFText = 4,
    SpriteMask = 8,
    Terrain = 16,
    All = -1,
}

public enum RenderTextureSubElement
{
    Color = 0,
    Depth = 1,
    Stencil = 2,
    Default = 0
}

public enum BuiltinRenderTextureType
{
    PropertyName = -4,
    BufferPtr = -3,
    RenderTexture = -2,
    BindableTexture = -1,
    None = 0,
    CurrentActive = 1,
    CameraTarget = 2,
    Depth = 3,
    DepthNormals = 4,
    ResolvedDepth = 5,
    PrepassNormalsSpec = 7,
    PrepassLight = 8,
    PrepassLightSpec = 9,
    GBuffer0 = 10,
    GBuffer1 = 11,
    GBuffer2 = 12,
    GBuffer3 = 13,
    Reflections = 14,
    MotionVectors = 15,
    GBuffer4 = 16,
    GBuffer5 = 17,
    GBuffer6 = 18,
    GBuffer7 = 19
}

public enum RenderBufferLoadAction
{
    Load = 0,
    Clear = 1,
    DontCare = 2
}

public enum RenderBufferStoreAction
{
    Store = 0,
    Resolve = 1,
    StoreAndResolve = 2,
    DontCare = 3
}

public enum TextureWrapMode
{
    Repeat = 0,
    Clamp = 1,
    Mirror = 2,
    MirrorOnce = 3
}

public enum FilterMode
{
    Point = 0,
    Bilinear = 1,
    Trilinear = 2
}

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
    ATC_RGB4 = 35,
    ETC2_RGB = 45,
    ETC2_RGBA8 = 46,
    ASTC_4x4 = 48,
    ASTC_5x5 = 49,
    ASTC_6x6 = 50,
    ASTC_8x8 = 51,
    ASTC_10x10 = 52,
    ASTC_12x12 = 53,
    RG16 = 62,
    R8 = 63,
    RG32 = 67,
    RGBA64 = 68,
    R8G8B8A8_SRGB = 69,
    ASTC_HDR_4x4 = 72
}

[Flags]
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

[Flags]
public enum HDRDisplaySupportFlags
{
    None = 0,
    Supported = 1,
    RuntimeSwitchable = 2,
    AutomaticTonemapping = 4
}

public enum CopyTextureSupport
{
    None = 0,
    Basic = 1,
    Copy3D = 2,
    DifferentTypes = 4,
    TextureToRT = 8,
    RTToTexture = 16
}

public enum LightProbeUsage
{
    Off = 0,
    BlendProbes = 1,
    UseProxyVolume = 2,
    CustomProvided = 4
}

public enum ReflectionProbeUsage
{
    Off = 0,
    BlendProbes = 1,
    BlendProbesAndSkybox = 2,
    Simple = 3
}

public enum ShadowCastingMode
{
    Off = 0,
    On = 1,
    TwoSided = 2,
    ShadowsOnly = 3
}

public enum RenderMode
{
    ScreenSpaceOverlay = 0,
    ScreenSpaceCamera = 1,
    WorldSpace = 2
}

public enum ProjectionType
{
    Perspective = 0,
    Orthographic = 1
}

public enum MeshTopology
{
    Triangles = 0,
    Quads = 2,
    Lines = 3,
    LineStrip = 4,
    Points = 5
}

public enum HideFlags
{
    None = 0,
    HideInHierarchy = 1,
    HideInInspector = 2,
    DontSaveInEditor = 4,
    NotEditable = 8,
    DontSaveInBuild = 16,
    DontUnloadUnusedAsset = 32,
    DontSave = DontSaveInEditor | DontSaveInBuild | DontUnloadUnusedAsset,
    HideAndDontSave = HideInHierarchy | DontSaveInEditor | NotEditable | DontSaveInBuild | DontUnloadUnusedAsset
}

public enum SendMessageOptions
{
    RequireReceiver = 0,
    DontRequireReceiver = 1
}

public enum Space
{
    World = 0,
    Self = 1
}

public enum CullMode { Off = 0, Front = 1, Back = 2 }
public enum CompareFunction { Disabled = 0, Never = 1, Less = 2, Equal = 3, LessEqual = 4, Greater = 5, NotEqual = 6, GreaterEqual = 7, Always = 8 }
public enum BlendMode { Zero = 0, One = 1, DstColor = 2, SrcColor = 3, OneMinusDstColor = 4, SrcAlpha = 5, OneMinusSrcAlpha = 6, DstAlpha = 7, OneMinusDstAlpha = 8, SrcAlphaSaturate = 9, OneMinusSrcColor = 10 }
public enum BlendOp { Add = 0, Subtract = 1, ReverseSubtract = 2, Min = 3, Max = 4 }
public enum StencilOp { Keep = 0, Zero = 1, Replace = 2, IncrementSaturate = 3, DecrementSaturate = 4, Invert = 5, IncrementWrap = 6, DecrementWrap = 7 }
[Flags] public enum ColorWriteMask { None = 0, Alpha = 1, Blue = 2, Green = 4, Red = 8, All = Red | Green | Blue | Alpha }

public struct BlendState
{
    public static readonly BlendState Opaque = new() { sourceBlend = BlendMode.One, destinationBlend = BlendMode.Zero, blendMode = BlendOp.Add, enabled = false };
    public static readonly BlendState AlphaBlend = new() { sourceBlend = BlendMode.SrcAlpha, destinationBlend = BlendMode.OneMinusSrcAlpha, blendMode = BlendOp.Add, enabled = true };
    public static readonly BlendState PremultipliedAlpha = new() { sourceBlend = BlendMode.One, destinationBlend = BlendMode.OneMinusSrcAlpha, blendMode = BlendOp.Add, enabled = true };
    public static readonly BlendState Additive = new() { sourceBlend = BlendMode.SrcAlpha, destinationBlend = BlendMode.One, blendMode = BlendOp.Add, enabled = true };
    public static readonly BlendState Multiply = new() { sourceBlend = BlendMode.DstColor, destinationBlend = BlendMode.Zero, blendMode = BlendOp.Add, enabled = true };
    public bool enabled;
    public BlendMode sourceBlend;
    public BlendMode destinationBlend;
    public BlendMode sourceBlendAlpha;
    public BlendMode destinationBlendAlpha;
    public BlendOp blendMode;
    public BlendOp blendModeAlpha;
    public ColorWriteMask writeMask;
    public bool separateMRTBlend;
    public BlendState SeparateAlpha(BlendMode srcA, BlendMode dstA, BlendOp opA) { sourceBlendAlpha = srcA; destinationBlendAlpha = dstA; blendModeAlpha = opA; return this; }
}

public struct DepthState
{
    public static readonly DepthState Default = new() { writeEnabled = true, compareFunction = CompareFunction.LessEqual };
    public static readonly DepthState DepthOff = new() { writeEnabled = false, compareFunction = CompareFunction.Always };
    public static readonly DepthState DepthRead = new() { writeEnabled = false, compareFunction = CompareFunction.LessEqual };
    public bool writeEnabled;
    public CompareFunction compareFunction;
}

public struct RasterState
{
    public static readonly RasterState Default = new() { cullingMode = CullMode.Back, depthBias = 0f, slopeDepthBias = 0f };
    public CullMode cullingMode;
    public float depthBias;
    public float slopeDepthBias;
    public bool conservative;
}

public struct StencilState
{
    public static readonly StencilState Default = new()
    {
        readMask = 255, writeMask = 255,
        compareFunctionFront = CompareFunction.Always, passOperationFront = StencilOp.Keep, failOperationFront = StencilOp.Keep, zFailOperationFront = StencilOp.Keep,
        compareFunctionBack = CompareFunction.Always, passOperationBack = StencilOp.Keep, failOperationBack = StencilOp.Keep, zFailOperationBack = StencilOp.Keep
    };
    public byte readMask;
    public byte writeMask;
    public CompareFunction compareFunctionFront;
    public StencilOp passOperationFront;
    public StencilOp failOperationFront;
    public StencilOp zFailOperationFront;
    public CompareFunction compareFunctionBack;
    public StencilOp passOperationBack;
    public StencilOp failOperationBack;
    public StencilOp zFailOperationBack;
    public bool enabled;
}

[Flags]
public enum PerObjectData
{
    None = 0,
    LightProbe = 1 << 0,
    ReflectionProbes = 1 << 1,
    LightProbeProxyVolume = 1 << 2,
    Lightmaps = 1 << 3,
    LightData = 1 << 4,
    MotionVectors = 1 << 5,
    ReflectionProbeData = 1 << 7,
    ShadowMask = 1 << 10,
    OcclusionProbe = 1 << 8,
    OcclusionProbeProxyVolume = 1 << 9,
}

[Flags]
public enum SortingCriteria
{
    None = 0,
    SortingLayer = 1 << 0,
    RenderQueue = 1 << 1,
    BackToFront = 1 << 2,
    FrontToBack = 1 << 3,
    QuantizedFrontToBack = 1 << 4,
    OptimizeStateChanges = 1 << 5,
    CanvasOrder = 1 << 6,
    CommonOpaque = SortingLayer | RenderQueue | FrontToBack | OptimizeStateChanges,
    CommonTransparent = SortingLayer | RenderQueue | BackToFront | OptimizeStateChanges,
    AllAlpha = BackToFront | FrontToBack | QuantizedFrontToBack,
    All = 0x7F
}

public enum DistanceMetric
{
    Perspective = 0,
    Orthographic = 1,
    Default = 0,
    CustomAxis = 2
}

public struct VisibleLight
{
    public LightType lightType;
    public Light light;
    public Color finalColor;
    public Matrix4x4 localToWorldMatrix;
    public Matrix4x4 worldToLocalMatrix;
    public Rect screenRect;
    public bool intersectsFarPlane;
    public bool intersectsNearPlane;
    public bool visible;
    public float range;
    public float spotAngle;
    public float intensity;

    public Vector3 lightPosition
    {
        get
        {
            var v = localToWorldMatrix.GetColumn(3);
            return new Vector3(v.x, v.y, v.z);
        }
    }

    public Vector3 lightDirection
    {
        get
        {
            var v = -localToWorldMatrix.GetColumn(2);
            return new Vector3(v.x, v.y, v.z).normalized;
        }
    }
}

public struct VisibleReflectionProbe
{
    public Bounds bounds;
    public ReflectionProbe probe;
    public Texture texture;
    public float blendDistance;
    public int importance;
    public bool boxProjection;
    public bool hdr;
    public Vector3 center;
    public Vector3 extents;
    public int probeIndex;
}

public struct SphericalHarmonicsL2
{
    public float shAr, shAg, shAb;
    public float shBr, shBg, shBb;
    public float shC;
    public float shDr, shDg, shDb;
    public float shEr, shEg, shEb;
    public float shFr, shFg, shFb;
    public float shGr, shGg, shGb;
    public float shHr, shHg, shHb;
    public float shIr, shIg, shIb;

    public float this[int rgb, int coefficient]
    {
        get => (rgb, coefficient) switch
        {
            (0, 0) => shAr, (0, 1) => shBr, (0, 2) => shC, (0, 3) => shDr, (0, 4) => shEr, (0, 5) => shFr, (0, 6) => shGr, (0, 7) => shHr, (0, 8) => shIr,
            (1, 0) => shAg, (1, 1) => shBg, (1, 2) => shC, (1, 3) => shDg, (1, 4) => shEg, (1, 5) => shFg, (1, 6) => shGg, (1, 7) => shHg, (1, 8) => shIg,
            (2, 0) => shAb, (2, 1) => shBb, (2, 2) => shC, (2, 3) => shDb, (2, 4) => shEb, (2, 5) => shFb, (2, 6) => shGb, (2, 7) => shHb, (2, 8) => shIb,
            _ => 0f
        };
        set
        {
            switch (rgb, coefficient)
            {
                case (0, 0): shAr = value; break; case (0, 1): shBr = value; break; case (0, 2): shC = value; break; case (0, 3): shDr = value; break; case (0, 4): shEr = value; break; case (0, 5): shFr = value; break; case (0, 6): shGr = value; break; case (0, 7): shHr = value; break; case (0, 8): shIr = value; break;
                case (1, 0): shAg = value; break; case (1, 1): shBg = value; break; case (1, 2): shC = value; break; case (1, 3): shDg = value; break; case (1, 4): shEg = value; break; case (1, 5): shFg = value; break; case (1, 6): shGg = value; break; case (1, 7): shHg = value; break; case (1, 8): shIg = value; break;
                case (2, 0): shAb = value; break; case (2, 1): shBb = value; break; case (2, 2): shC = value; break; case (2, 3): shDb = value; break; case (2, 4): shEb = value; break; case (2, 5): shFb = value; break; case (2, 6): shGb = value; break; case (2, 7): shHb = value; break; case (2, 8): shIb = value; break;
            }
        }
    }

    public void AddAmbientLight(Color color)
    {
        float r = color.r, g = color.g, b = color.b;
        shAr += r; shAg += g; shAb += b;
    }

    public void Clear() { shAr = shAg = shAb = shBr = shBg = shBb = shC = shDr = shDg = shDb = shEr = shEg = shEb = shFr = shFg = shFb = shGr = shGg = shGb = shHr = shHg = shHb = shIr = shIg = shIb = 0f; }
}

public struct RenderTargetIdentifier
{
    public int m_NameID;
    public IntPtr m_BufferPtr;
    public int m_MipLevel;
    public CubemapFace m_CubeFace;
    public int m_DepthSlice;

    public RenderTargetIdentifier(Texture tex)
    {
        m_NameID = tex != null ? tex.GetInstanceID() : 0;
        m_BufferPtr = IntPtr.Zero;
        m_MipLevel = 0;
        m_CubeFace = CubemapFace.Unknown;
        m_DepthSlice = 0;
    }

    public RenderTargetIdentifier(RenderTexture tex)
    {
        m_NameID = tex != null ? tex.GetInstanceID() : 0;
        m_BufferPtr = IntPtr.Zero;
        m_MipLevel = 0;
        m_CubeFace = CubemapFace.Unknown;
        m_DepthSlice = 0;
    }

    public RenderTargetIdentifier(BuiltinRenderTextureType type)
    {
        m_NameID = (int)type;
        m_BufferPtr = IntPtr.Zero;
        m_MipLevel = 0;
        m_CubeFace = CubemapFace.Unknown;
        m_DepthSlice = 0;
    }

    public RenderTargetIdentifier(string name)
    {
        m_NameID = Shader.PropertyToID(name);
        m_BufferPtr = IntPtr.Zero;
        m_MipLevel = 0;
        m_CubeFace = CubemapFace.Unknown;
        m_DepthSlice = 0;
    }

    public RenderTargetIdentifier(int nameID)
    {
        m_NameID = nameID;
        m_BufferPtr = IntPtr.Zero;
        m_MipLevel = 0;
        m_CubeFace = CubemapFace.Unknown;
        m_DepthSlice = 0;
    }

    public static implicit operator RenderTargetIdentifier(BuiltinRenderTextureType type) => new(type);
    public static implicit operator RenderTargetIdentifier(Texture tex) => new(tex);
    public static implicit operator RenderTargetIdentifier(string name) => new(name);
    public static implicit operator RenderTargetIdentifier(int nameID) => new(nameID);
}

public enum CubemapFace
{
    Unknown = -1,
    PositiveX = 0,
    NegativeX = 1,
    PositiveY = 2,
    NegativeY = 3,
    PositiveZ = 4,
    NegativeZ = 5
}

public enum ComputeBufferMode { Immutable, Dynamic, Circular, SubUpdates }
public enum ComputeBufferType { Default = 0, Raw = 1, Append = 2, Counter = 4, IndirectArguments = 8, Structured = 16, DrawIndirect = 256 }
public enum CubemapFace2 { }
public enum LightShadowCasterMode { Default, NonLightmappedOnly, Everything }

[Flags]
public enum RenderStateMask { Nothing = 0, Blend = 1, Raster = 2, Depth = 4, Stencil = 8, Everything = 15 }

