using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering;

public struct ScriptableCullingParameters
{
    public Camera camera;
    public float shadowDistance;
}

public static class GraphicsSettings
{
    private static RenderPipelineAsset? _defaultRenderPipeline;
    private static RenderPipeline? _currentPipelineInstance;
    private static readonly List<Action<RenderPipelineAsset, RenderPipelineAsset>> _renderPipelineChangedListeners = new();

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

    public static void RegisterRenderPipelineSettings<T>(T settings) where T : RenderPipelineAsset { }
    public static void UnregisterRenderPipelineSettings<T>(T settings) where T : RenderPipelineAsset { }

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

public static class QualitySettings
{
    private static RenderPipelineAsset? _renderPipeline;
    private static int _currentQualityLevel;
    private static readonly string[] _names = { "Low", "Medium", "High", "Ultra" };

    public static RenderPipelineAsset? renderPipeline
    {
        get => _renderPipeline;
        set
        {
            if (_renderPipeline == value) return;
            var old = _renderPipeline;
            _renderPipeline = value;
            GraphicsSettings.OnQualityPipelineChanged(old, value);
        }
    }

    public static int pixelLightCount { get; set; } = 4;
    public static float shadowDistance { get; set; } = 150f;
    public static float shadowNearPlaneOffset { get; set; } = 3f;
    public static int shadowCascades { get; set; } = 1;
    public static float shadowCascade2Split { get; set; } = 1f / 3f;
    public static Vector3 shadowCascade4Split { get; set; } = new(0.067f, 0.2f, 0.467f);
    public static ShadowResolution shadowResolution { get; set; } = ShadowResolution.Medium;
    public static ShadowProjection shadowProjection { get; set; } = ShadowProjection.CloseFit;
    public static ShadowQuality shadows { get; set; } = ShadowQuality.All;
    public static ShadowmaskMode shadowmaskMode { get; set; } = ShadowmaskMode.DistanceShadowmask;
    public static bool softParticles { get; set; }
    public static bool softVegetation { get; set; } = true;
    public static bool realtimeReflectionProbes { get; set; } = true;
    public static bool billboardsFaceCameraPosition { get; set; } = true;
    public static int vSyncCount { get; set; } = 1;
    public static int antiAliasing { get; set; }
    public static MSAA antiAliasingValue => (MSAA)(antiAliasing > 0 ? antiAliasing : 1);
    public static float lodBias { get; set; } = 2f;
    public static int maximumLODLevel { get; set; }
    public static AnisotropicFiltering anisotropicFiltering { get; set; } = AnisotropicFiltering.Enable;
    public static int masterTextureLimit { get; set; }
    public static int particleRaycastBudget { get; set; } = 256;
    public static int asyncUploadTimeSlice { get; set; } = 2;
    public static int asyncUploadBufferSize { get; set; } = 16;
    public static bool streamingMipmapsActive { get; set; }
    public static bool streamingMipmapsAddAllCameras { get; set; } = true;
    public static float streamingMipmapsMemoryBudget { get; set; } = 512f;
    public static int streamingMipmapsRenderersPerFrame { get; set; } = 512;
    public static int streamingMipmapsMaxLevelReduction { get; set; } = 2;
    public static int streamingMipmapsMaxFileIORequests { get; set; } = 1024;
    public static string[] names => _names;
    public static int count => _names.Length;

    public static ColorSpace activeColorSpace { get; set; } = ColorSpace.Gamma;
    public static ColorSpace desiredColorSpace { get; set; } = ColorSpace.Gamma;
    public static bool hdr { get; set; }

    public static void SetQualityLevel(int index)
    {
        _currentQualityLevel = Mathf.Clamp(index, 0, _names.Length - 1);
    }

    public static void SetQualityLevel(int index, bool applyExpensiveChanges)
    {
        SetQualityLevel(index);
    }

    public static int GetQualityLevel() => _currentQualityLevel;
    public static void IncreaseLevel(bool applyExpensiveChanges = false) => SetQualityLevel(_currentQualityLevel + 1);
    public static void DecreaseLevel(bool applyExpensiveChanges = false) => SetQualityLevel(_currentQualityLevel - 1);
}

public enum TransparencySortMode
{
    Default = 0,
    Perspective = 1,
    Orthographic = 2,
    CustomAxis = 3
}

public enum ShadowResolution
{
    Low = 0,
    Medium = 1,
    High = 2,
    VeryHigh = 3
}

public enum ShadowProjection
{
    CloseFit = 0,
    StableFit = 1
}

public enum ShadowQuality
{
    Disable = 0,
    HardShadows = 1,
    All = 2
}

public enum ShadowmaskMode
{
    Shadowmask = 0,
    DistanceShadowmask = 1
}

public enum AnisotropicFiltering
{
    Disable = 0,
    Enable = 1,
    ForceEnable = 2
}

public enum MSAA
{
    None = 1,
    _2x = 2,
    _4x = 4,
    _8x = 8
}

public enum ColorSpace
{
    Uninitialized = -1,
    Gamma = 0,
    Linear = 1
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

