using UnityEditor.ShaderGraph.Generation;
using UnityEditor.ShaderGraph.Model;
using UnityEditor.ShaderGraph.Serialization;
using Xunit;

namespace Unity.ShaderGraph.Editor.Tests;

public sealed class UniversalPassPlannerTests
{
    [Fact]
    public void UnlitOpaque_DefaultPlanMatchesUrp14Order()
    {
        UniversalPassPlan plan = UniversalPassPlanner.Create(Target(UniversalUnlit));

        Assert.Equal(
            new[]
            {
                "Universal Forward", "DepthOnly", "DepthNormalsOnly", "ShadowCaster",
                "GBuffer", "SceneSelectionPass", "ScenePickingPass"
            },
            plan.Passes.Select(pass => pass.DisplayName));
    }

    [Fact]
    public void UnlitForward_UsesSrpDefaultLightModeAndExactIdentity()
    {
        UniversalPassDescriptor pass = UniversalPassPlanner.Create(Target(UniversalUnlit)).Passes[0];

        Assert.Equal("Universal Forward", pass.DisplayName);
        Assert.Equal("SHADERPASS_UNLIT", pass.ReferenceName);
        Assert.Equal(string.Empty, pass.LightMode);
        Assert.True(pass.UseInPreview);
        Assert.Equal(UniversalPragmaProfile.Forward, pass.Pragmas);
        Assert.Equal(UniversalIncludeProfile.UnlitForward, pass.Includes);
        Assert.Equal(
            "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Templates/ShaderPass.template",
            UniversalPassDescriptor.TemplatePath);
    }

    [Fact]
    public void UnlitTransparentAuto_OmitsDepthOnly()
    {
        UniversalPassPlan plan = UniversalPassPlanner.Create(Target(
            UniversalUnlit,
            "\"m_SurfaceType\":1,\"m_ZWriteControl\":0"));

        Assert.DoesNotContain(plan.Passes, pass => pass.DisplayName == "DepthOnly");
        Assert.Contains(plan.Passes, pass => pass.DisplayName == "DepthNormalsOnly");
    }

    [Fact]
    public void UnlitCastShadowsFalse_OmitsShadowCaster()
    {
        UniversalPassPlan plan = UniversalPassPlanner.Create(Target(
            UniversalUnlit,
            "\"m_CastShadows\":false"));

        Assert.DoesNotContain(plan.Passes, pass => pass.DisplayName == "ShadowCaster");
    }

    [Fact]
    public void MaterialOverride_ForcesDepthAndShadowVariants()
    {
        UniversalPassPlan plan = UniversalPassPlanner.Create(Target(
            UniversalUnlit,
            "\"m_SurfaceType\":1,\"m_ZWriteControl\":2," +
            "\"m_CastShadows\":false,\"m_AllowMaterialOverride\":true"));

        Assert.Contains(plan.Passes, pass => pass.DisplayName == "DepthOnly");
        Assert.Contains(plan.Passes, pass => pass.DisplayName == "ShadowCaster");
    }

    [Theory]
    [InlineData(0, 0, false, true)]
    [InlineData(1, 0, false, false)]
    [InlineData(1, 1, false, true)]
    [InlineData(0, 2, false, false)]
    [InlineData(1, 2, true, true)]
    public void MayWriteDepth_MatchesUniversalTarget(
        int surfaceType,
        int zWriteControl,
        bool allowOverride,
        bool expected)
    {
        ShaderGraphTarget target = Target(
            UniversalUnlit,
            $"\"m_SurfaceType\":{surfaceType},\"m_ZWriteControl\":{zWriteControl}," +
            $"\"m_AllowMaterialOverride\":{allowOverride.ToString().ToLowerInvariant()}");

        Assert.Equal(expected, UniversalPassPlanner.MayWriteDepth(target));
    }

    [Fact]
    public void Lit_DefaultPlanMatchesUrp14Order()
    {
        UniversalPassPlan plan = UniversalPassPlanner.Create(Target(UniversalLit));

        Assert.Equal(
            new[]
            {
                "Universal Forward", "GBuffer", "ShadowCaster", "DepthOnly", "DepthNormals",
                "Meta", "SceneSelectionPass", "ScenePickingPass", "Universal 2D"
            },
            plan.Passes.Select(pass => pass.DisplayName));
    }

    [Fact]
    public void LitClearCoat_UsesComplexLitForwardOnlyAndDepthNormalsOnly()
    {
        UniversalPassPlan plan = UniversalPassPlanner.Create(Target(
            UniversalLit,
            null,
            "\"m_ClearCoat\":true"));

        Assert.Equal("SHADERPASS_FORWARDONLY", plan.Passes[0].ReferenceName);
        Assert.Equal("UniversalForwardOnly", plan.Passes[0].LightMode);
        Assert.Contains(plan.Passes, pass => pass.ReferenceName == "SHADERPASS_DEPTHNORMALSONLY");
        Assert.DoesNotContain(plan.Passes, pass => pass.ReferenceName == "SHADERPASS_DEPTHNORMALS");
    }

    [Fact]
    public void LitTransparentForceDisabledAndNoShadow_OmitsBothConditionalPasses()
    {
        UniversalPassPlan plan = UniversalPassPlanner.Create(Target(
            UniversalLit,
            "\"m_SurfaceType\":1,\"m_ZWriteControl\":2,\"m_CastShadows\":false"));

        Assert.Equal(7, plan.Passes.Count);
        Assert.DoesNotContain(plan.Passes, pass => pass.DisplayName == "DepthOnly");
        Assert.DoesNotContain(plan.Passes, pass => pass.DisplayName == "ShadowCaster");
    }

    [Fact]
    public void LitPassIdentitiesAndProfiles_AreExact()
    {
        UniversalPassPlan plan = UniversalPassPlanner.Create(Target(UniversalLit));
        UniversalPassDescriptor gbuffer = plan.Passes.Single(pass => pass.DisplayName == "GBuffer");
        UniversalPassDescriptor meta = plan.Passes.Single(pass => pass.DisplayName == "Meta");
        UniversalPassDescriptor picking = plan.Passes.Single(pass => pass.DisplayName == "ScenePickingPass");

        Assert.Equal("SHADERPASS_GBUFFER", gbuffer.ReferenceName);
        Assert.Equal("UniversalGBuffer", gbuffer.LightMode);
        Assert.Equal(UniversalPragmaProfile.GBuffer, gbuffer.Pragmas);
        Assert.Equal(UniversalIncludeProfile.LitGBuffer, gbuffer.Includes);
        Assert.Equal("SHADERPASS_META", meta.ReferenceName);
        Assert.Equal("Meta", meta.LightMode);
        Assert.False(meta.UseInPreview);
        Assert.Equal("Picking", picking.LightMode);
    }

    [Fact]
    public void BuiltInTarget_IsRejectedByUrpPlanner()
    {
        ShaderGraphTarget target = Target(
            "UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInUnlitSubTarget",
            null,
            null,
            "UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInTarget");

        Assert.Throws<NotSupportedException>(() => UniversalPassPlanner.Create(target));
    }

    [Theory]
    [InlineData(UniversalDecal)]
    [InlineData(UniversalFullscreen)]
    public void RecognizedButUnimplementedUrpSubTargets_AreRejected(string subTargetType)
    {
        Assert.Throws<NotSupportedException>(() => UniversalPassPlanner.Create(Target(subTargetType)));
    }

    private static ShaderGraphTarget Target(
        string subTargetType,
        string? targetFields = null,
        string? subTargetFields = null,
        string targetType = UniversalTarget)
    {
        string graph = "{\"m_SGVersion\":3,\"m_Type\":\"UnityEditor.ShaderGraph.GraphData\"," +
                       "\"m_ObjectId\":\"graph\",\"m_Nodes\":[]," +
                       "\"m_ActiveTargets\":[{\"m_Id\":\"target\"}]}";
        string target = Document(
            targetType,
            "target",
            "\"m_ActiveSubTarget\":{\"m_Id\":\"sub\"}",
            targetFields);
        string subTarget = Document(subTargetType, "sub", subTargetFields);
        MultiJsonAsset asset = MultiJsonAsset.Parse(string.Join("\n\n", graph, target, subTarget));
        return Assert.Single(ShaderGraphTargetSet.Create(asset).Targets);
    }

    private static string Document(string type, string id, params string?[] fields)
    {
        string suffix = string.Concat(fields.Where(field => !string.IsNullOrWhiteSpace(field)).Select(field => "," + field));
        return $"{{\"m_SGVersion\":0,\"m_Type\":\"{type}\",\"m_ObjectId\":\"{id}\"{suffix}}}";
    }

    private const string UniversalTarget = "UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget";
    private const string UniversalLit = "UnityEditor.Rendering.Universal.ShaderGraph.UniversalLitSubTarget";
    private const string UniversalUnlit = "UnityEditor.Rendering.Universal.ShaderGraph.UniversalUnlitSubTarget";
    private const string UniversalDecal = "UnityEditor.Rendering.Universal.ShaderGraph.UniversalDecalSubTarget";
    private const string UniversalFullscreen = "UnityEditor.Rendering.Universal.ShaderGraph.UniversalFullscreenSubTarget";
}
