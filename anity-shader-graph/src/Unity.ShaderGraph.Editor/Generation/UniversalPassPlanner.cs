using System.Collections.ObjectModel;
using UnityEditor.ShaderGraph.Model;

namespace UnityEditor.ShaderGraph.Generation;

internal enum UniversalPragmaProfile
{
    Default,
    Instanced,
    Forward,
    GBuffer
}

internal enum UniversalIncludeProfile
{
    UnlitForward,
    UnlitGBuffer,
    LitForward,
    LitGBuffer,
    DepthOnly,
    DepthNormalsOnly,
    ShadowCaster,
    Meta,
    SceneSelection,
    ScenePicking,
    Lit2D
}

internal sealed class UniversalPassDescriptor
{
    internal UniversalPassDescriptor(
        string displayName,
        string referenceName,
        string lightMode,
        bool useInPreview,
        UniversalPragmaProfile pragmas,
        UniversalIncludeProfile includes)
    {
        DisplayName = displayName;
        ReferenceName = referenceName;
        LightMode = lightMode;
        UseInPreview = useInPreview;
        Pragmas = pragmas;
        Includes = includes;
    }

    internal string DisplayName { get; }

    internal string ReferenceName { get; }

    internal string LightMode { get; }

    internal bool UseInPreview { get; }

    internal UniversalPragmaProfile Pragmas { get; }

    internal UniversalIncludeProfile Includes { get; }

    internal const string TemplatePath =
        "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Templates/ShaderPass.template";
}

internal sealed class UniversalPassPlan
{
    private readonly ReadOnlyCollection<UniversalPassDescriptor> _passes;

    internal UniversalPassPlan(ShaderGraphTarget target, List<UniversalPassDescriptor> passes)
    {
        Target = target;
        _passes = passes.AsReadOnly();
    }

    internal ShaderGraphTarget Target { get; }

    internal IReadOnlyList<UniversalPassDescriptor> Passes => _passes;
}

/// <summary>
/// Reproduces the URP 14 Lit/Unlit SubShader pass order and culling conditions.
/// Decal and Fullscreen are intentionally rejected until their dedicated templates are implemented.
/// </summary>
internal static class UniversalPassPlanner
{
    internal static UniversalPassPlan Create(ShaderGraphTarget target)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (target.Kind != ShaderGraphTargetKind.Universal)
            throw new NotSupportedException("URP pass planning requires a UniversalTarget.");
        return target.SubTarget.Kind switch
        {
            ShaderGraphSubTargetKind.UniversalLit => CreateLit(target),
            ShaderGraphSubTargetKind.UniversalUnlit => CreateUnlit(target),
            ShaderGraphSubTargetKind.UniversalDecal => throw new NotSupportedException(
                "UniversalDecalSubTarget pass planning is not implemented."),
            ShaderGraphSubTargetKind.UniversalFullscreen => throw new NotSupportedException(
                "UniversalFullscreenSubTarget pass planning is not implemented."),
            _ => throw new NotSupportedException(
                "Unsupported Universal sub-target '" + target.SubTarget.Kind + "'.")
        };
    }

    internal static bool MayWriteDepth(ShaderGraphTarget target)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (target.AllowMaterialOverride) return true;
        return target.ZWriteControl switch
        {
            ShaderGraphZWriteControl.Auto => target.SurfaceType == ShaderGraphSurfaceType.Opaque,
            ShaderGraphZWriteControl.ForceDisabled => false,
            ShaderGraphZWriteControl.ForceEnabled => true,
            _ => throw new InvalidDataException("UniversalTarget has no valid ZWriteControl.")
        };
    }

    private static UniversalPassPlan CreateUnlit(ShaderGraphTarget target)
    {
        var passes = new List<UniversalPassDescriptor>
        {
            Pass("Universal Forward", "SHADERPASS_UNLIT", "", true,
                UniversalPragmaProfile.Forward, UniversalIncludeProfile.UnlitForward)
        };
        if (MayWriteDepth(target))
            passes.Add(Pass("DepthOnly", "SHADERPASS_DEPTHONLY", "DepthOnly", true,
                UniversalPragmaProfile.Instanced, UniversalIncludeProfile.DepthOnly));
        passes.Add(Pass("DepthNormalsOnly", "SHADERPASS_DEPTHNORMALSONLY", "DepthNormalsOnly", true,
            UniversalPragmaProfile.Instanced, UniversalIncludeProfile.DepthNormalsOnly));
        if (target.CastShadows || target.AllowMaterialOverride)
            passes.Add(Pass("ShadowCaster", "SHADERPASS_SHADOWCASTER", "ShadowCaster", false,
                UniversalPragmaProfile.Instanced, UniversalIncludeProfile.ShadowCaster));
        passes.Add(Pass("GBuffer", "SHADERPASS_GBUFFER", "UniversalGBuffer", true,
            UniversalPragmaProfile.GBuffer, UniversalIncludeProfile.UnlitGBuffer));
        passes.Add(Pass("SceneSelectionPass", "SHADERPASS_DEPTHONLY", "SceneSelectionPass", false,
            UniversalPragmaProfile.Default, UniversalIncludeProfile.SceneSelection));
        passes.Add(Pass("ScenePickingPass", "SHADERPASS_DEPTHONLY", "Picking", false,
            UniversalPragmaProfile.Default, UniversalIncludeProfile.ScenePicking));
        return new UniversalPassPlan(target, passes);
    }

    private static UniversalPassPlan CreateLit(ShaderGraphTarget target)
    {
        bool complexLit = target.SubTarget.ClearCoat;
        var passes = new List<UniversalPassDescriptor>
        {
            complexLit
                ? Pass("Universal Forward Only", "SHADERPASS_FORWARDONLY", "UniversalForwardOnly", true,
                    UniversalPragmaProfile.Forward, UniversalIncludeProfile.LitForward)
                : Pass("Universal Forward", "SHADERPASS_FORWARD", "UniversalForward", true,
                    UniversalPragmaProfile.Forward, UniversalIncludeProfile.LitForward),
            Pass("GBuffer", "SHADERPASS_GBUFFER", "UniversalGBuffer", true,
                UniversalPragmaProfile.GBuffer, UniversalIncludeProfile.LitGBuffer)
        };
        if (target.CastShadows || target.AllowMaterialOverride)
            passes.Add(Pass("ShadowCaster", "SHADERPASS_SHADOWCASTER", "ShadowCaster", false,
                UniversalPragmaProfile.Instanced, UniversalIncludeProfile.ShadowCaster));
        if (MayWriteDepth(target))
            passes.Add(Pass("DepthOnly", "SHADERPASS_DEPTHONLY", "DepthOnly", true,
                UniversalPragmaProfile.Instanced, UniversalIncludeProfile.DepthOnly));
        passes.Add(complexLit
            ? Pass("DepthNormalsOnly", "SHADERPASS_DEPTHNORMALSONLY", "DepthNormalsOnly", true,
                UniversalPragmaProfile.Instanced, UniversalIncludeProfile.DepthNormalsOnly)
            : Pass("DepthNormals", "SHADERPASS_DEPTHNORMALS", "DepthNormals", true,
                UniversalPragmaProfile.Instanced, UniversalIncludeProfile.DepthNormalsOnly));
        passes.Add(Pass("Meta", "SHADERPASS_META", "Meta", false,
            UniversalPragmaProfile.Default, UniversalIncludeProfile.Meta));
        passes.Add(Pass("SceneSelectionPass", "SHADERPASS_DEPTHONLY", "SceneSelectionPass", false,
            UniversalPragmaProfile.Default, UniversalIncludeProfile.SceneSelection));
        passes.Add(Pass("ScenePickingPass", "SHADERPASS_DEPTHONLY", "Picking", false,
            UniversalPragmaProfile.Default, UniversalIncludeProfile.ScenePicking));
        passes.Add(Pass("Universal 2D", "SHADERPASS_2D", "Universal2D", false,
            UniversalPragmaProfile.Default, UniversalIncludeProfile.Lit2D));
        return new UniversalPassPlan(target, passes);
    }

    private static UniversalPassDescriptor Pass(
        string displayName,
        string referenceName,
        string lightMode,
        bool useInPreview,
        UniversalPragmaProfile pragmas,
        UniversalIncludeProfile includes)
        => new(displayName, referenceName, lightMode, useInPreview, pragmas, includes);
}
