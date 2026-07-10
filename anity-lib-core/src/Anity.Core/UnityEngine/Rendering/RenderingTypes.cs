using System;

namespace UnityEngine.Rendering;

public static class GraphicsSettings
{
    public static RenderPipelineAsset currentRenderPipeline { get; set; }
    public static Shader defaultShader { get; set; }
    public static Material defaultSpatialMaterial { get; set; }
    public static bool useScriptableRenderPipeline => currentRenderPipeline != null;

    public static int shaderRenderPipelineAssetCount { get; }
    public static int customRenderPipelineAssetCount { get; }

    public static void RegisterRenderPipelineSettings<T>(T settings) where T : RenderPipelineAsset { }
    public static void UnregisterRenderPipelineSettings<T>(T settings) where T : RenderPipelineAsset { }
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

[Flags]
public enum RenderingLayerMask
{
    Default = 1,
    Everything = ~0,
    Nothing = 0,
}

public struct RenderingLayerMaskValue
{
    public uint value;
    public static RenderingLayerMaskValue everything => new RenderingLayerMaskValue { value = uint.MaxValue };
    public static RenderingLayerMaskValue nothing => new RenderingLayerMaskValue { value = 0 };
}

public enum ShaderRenderPipeline
{
    BuiltIn = 0,
    Universal = 1,
    HD = 2,
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


