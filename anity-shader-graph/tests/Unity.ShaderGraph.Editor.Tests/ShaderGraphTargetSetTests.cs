using UnityEditor.ShaderGraph.Model;
using UnityEditor.ShaderGraph.Serialization;
using Xunit;

namespace Unity.ShaderGraph.Editor.Tests;

public sealed class ShaderGraphTargetSetTests
{
    [Fact]
    public void Create_GraphWithoutTargets_ReturnsEmptySet()
    {
        ShaderGraphTargetSet targets = ShaderGraphTargetSet.Create(Parse(Graph(Array.Empty<string>())));

        Assert.Empty(targets.Targets);
        Assert.Empty(targets.ProductTargets);
    }

    [Fact]
    public void Create_UniversalLit_ParsesExactSettings()
    {
        string target = Target(
            UniversalTarget,
            "target",
            "sub",
            "\"m_SurfaceType\":1,\"m_ZWriteControl\":2,\"m_ZTestMode\":8," +
            "\"m_AlphaMode\":3,\"m_RenderFace\":0,\"m_AllowMaterialOverride\":true," +
            "\"m_AlphaClip\":true,\"m_CastShadows\":false,\"m_ReceiveShadows\":false," +
            "\"m_SupportsLODCrossFade\":true,\"m_SupportVFX\":true," +
            "\"m_CustomEditorGUI\":\"Custom.Gui\"");
        string sub = SubTarget(
            UniversalLit,
            "sub",
            "\"m_WorkflowMode\":0,\"m_NormalDropOffSpace\":2," +
            "\"m_BlendModePreserveSpecular\":false,\"m_ClearCoat\":true");
        ShaderGraphTarget parsed = Assert.Single(ShaderGraphTargetSet.Create(Parse(Graph("target"), target, sub)).Targets);

        Assert.Equal(ShaderGraphTargetKind.Universal, parsed.Kind);
        Assert.Equal(ShaderGraphSubTargetKind.UniversalLit, parsed.SubTarget.Kind);
        Assert.Equal(ShaderGraphSurfaceType.Transparent, parsed.SurfaceType);
        Assert.Equal(ShaderGraphZWriteControl.ForceDisabled, parsed.ZWriteControl);
        Assert.Equal(ShaderGraphZTestMode.Always, parsed.ZTestMode);
        Assert.Equal(ShaderGraphAlphaMode.Multiply, parsed.AlphaMode);
        Assert.Equal(ShaderGraphRenderFace.Both, parsed.RenderFace);
        Assert.True(parsed.AllowMaterialOverride);
        Assert.True(parsed.AlphaClip);
        Assert.False(parsed.CastShadows);
        Assert.False(parsed.ReceiveShadows);
        Assert.True(parsed.SupportsLodCrossFade);
        Assert.True(parsed.SupportVfx);
        Assert.Equal("Custom.Gui", parsed.CustomEditorGui);
        Assert.Equal(ShaderGraphWorkflowMode.Specular, parsed.SubTarget.WorkflowMode);
        Assert.Equal(ShaderGraphNormalDropOffSpace.World, parsed.SubTarget.NormalDropOffSpace);
        Assert.False(parsed.SubTarget.BlendModePreserveSpecular);
        Assert.True(parsed.SubTarget.ClearCoat);
        Assert.True(parsed.IsProductSupported);
    }

    [Fact]
    public void Create_UniversalDefaults_MatchUrp14()
    {
        ShaderGraphTarget target = Assert.Single(ShaderGraphTargetSet.Create(Parse(
            Graph("target"),
            Target(UniversalTarget, "target", "sub"),
            SubTarget(UniversalUnlit, "sub"))).Targets);

        Assert.Equal(ShaderGraphSurfaceType.Opaque, target.SurfaceType);
        Assert.Equal(ShaderGraphZWriteControl.Auto, target.ZWriteControl);
        Assert.Equal(ShaderGraphZTestMode.LEqual, target.ZTestMode);
        Assert.Equal(ShaderGraphAlphaMode.Alpha, target.AlphaMode);
        Assert.Equal(ShaderGraphRenderFace.Front, target.RenderFace);
        Assert.True(target.CastShadows);
        Assert.True(target.ReceiveShadows);
        Assert.False(target.AlphaClip);
        Assert.Equal(ShaderGraphWorkflowMode.Metallic, target.SubTarget.WorkflowMode);
        Assert.Equal(ShaderGraphNormalDropOffSpace.Tangent, target.SubTarget.NormalDropOffSpace);
        Assert.True(target.SubTarget.BlendModePreserveSpecular);
    }

    [Theory]
    [InlineData(UniversalUnlit, "UniversalUnlit")]
    [InlineData(UniversalDecal, "UniversalDecal")]
    [InlineData(UniversalFullscreen, "UniversalFullscreen")]
    public void Create_AllUrpSubTargets_AreProductSupported(string subTargetType, string expectedKind)
    {
        ShaderGraphTarget target = Assert.Single(ShaderGraphTargetSet.Create(Parse(
            Graph("target"),
            Target(UniversalTarget, "target", "sub"),
            SubTarget(subTargetType, "sub"))).Targets);

        Assert.Equal(Enum.Parse<ShaderGraphSubTargetKind>(expectedKind), target.SubTarget.Kind);
        Assert.True(target.IsProductSupported);
    }

    [Fact]
    public void Create_BuiltIn_IsPreservedButNotProductSupported()
    {
        ShaderGraphTarget target = Assert.Single(ShaderGraphTargetSet.Create(Parse(
            Graph("target"),
            Target(BuiltInTarget, "target", "sub"),
            SubTarget(BuiltInLit, "sub", "\"m_WorkflowMode\":1,\"m_NormalDropOffSpace\":0"))).Targets);

        Assert.Equal(ShaderGraphTargetKind.BuiltIn, target.Kind);
        Assert.Equal(ShaderGraphSubTargetKind.BuiltInLit, target.SubTarget.Kind);
        Assert.False(target.IsProductSupported);
    }

    [Fact]
    public void Create_HdrpAndDatas_ArePreservedButNotProductSupported()
    {
        string target = Target(HighDefinitionTarget, "target", "sub", "\"m_Datas\":[{\"m_Id\":\"data\"}]");
        ShaderGraphTarget parsed = Assert.Single(ShaderGraphTargetSet.Create(Parse(
            Graph("target"),
            target,
            SubTarget(HighDefinitionLit, "sub"),
            Document("Data", "data"))).Targets);

        Assert.Equal(ShaderGraphTargetKind.HighDefinition, parsed.Kind);
        Assert.Equal(new[] { "data" }, parsed.DataObjectIds);
        Assert.Null(parsed.SurfaceType);
        Assert.False(parsed.IsProductSupported);
    }

    [Fact]
    public void Create_MultipleTargets_PreservesGraphOrder()
    {
        ShaderGraphTargetSet set = ShaderGraphTargetSet.Create(Parse(
            Graph("builtIn", "urp"),
            Target(BuiltInTarget, "builtIn", "builtInSub"),
            SubTarget(BuiltInUnlit, "builtInSub"),
            Target(UniversalTarget, "urp", "urpSub"),
            SubTarget(UniversalUnlit, "urpSub")));

        Assert.Equal(new[] { "builtIn", "urp" }, set.Targets.Select(target => target.ObjectId));
        Assert.Equal("urp", Assert.Single(set.ProductTargets).ObjectId);
    }

    [Fact]
    public void Create_UniversalDecal_ParsesTypedData()
    {
        const string decal = "\"m_DecalData\":{" +
                             "\"affectsAlbedo\":false,\"affectsNormalBlend\":true," +
                             "\"affectsNormal\":false,\"affectsMAOS\":true," +
                             "\"affectsEmission\":true,\"drawOrder\":7," +
                             "\"supportLodCrossFade\":true,\"angleFade\":true}";
        ShaderGraphDecalData data = Assert.Single(ShaderGraphTargetSet.Create(Parse(
            Graph("target"),
            Target(UniversalTarget, "target", "sub"),
            SubTarget(UniversalDecal, "sub", decal))).Targets).SubTarget.DecalData!;

        Assert.False(data.AffectsAlbedo);
        Assert.True(data.AffectsNormalBlend);
        Assert.False(data.AffectsNormal);
        Assert.True(data.AffectsMaos);
        Assert.True(data.AffectsEmission);
        Assert.Equal(7, data.DrawOrder);
        Assert.True(data.SupportLodCrossFade);
        Assert.True(data.AngleFade);
    }

    [Fact]
    public void Create_UnknownCustomTarget_IsPreservedAsUnsupported()
    {
        ShaderGraphTarget target = Assert.Single(ShaderGraphTargetSet.Create(Parse(
            Graph("target"),
            Target("Custom.Target", "target", "sub"),
            SubTarget("Custom.SubTarget", "sub"))).Targets);

        Assert.Equal(ShaderGraphTargetKind.Unknown, target.Kind);
        Assert.Equal(ShaderGraphSubTargetKind.Unknown, target.SubTarget.Kind);
        Assert.False(target.IsProductSupported);
    }

    [Fact]
    public void Create_MissingTarget_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => ShaderGraphTargetSet.Create(Parse(Graph("missing"))));
    }

    [Fact]
    public void Create_DuplicateActiveTarget_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => ShaderGraphTargetSet.Create(Parse(
            Graph("target", "target"),
            Target(UniversalTarget, "target", "sub"),
            SubTarget(UniversalUnlit, "sub"))));
    }

    [Fact]
    public void Create_MissingSubTarget_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => ShaderGraphTargetSet.Create(Parse(
            Graph("target"),
            Target(UniversalTarget, "target", "missing"))));
    }

    [Fact]
    public void Create_IncompatibleTargetSubTarget_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => ShaderGraphTargetSet.Create(Parse(
            Graph("target"),
            Target(UniversalTarget, "target", "sub"),
            SubTarget(BuiltInUnlit, "sub"))));
    }

    [Theory]
    [InlineData("m_SurfaceType", 2)]
    [InlineData("m_ZWriteControl", 3)]
    [InlineData("m_ZTestMode", 9)]
    [InlineData("m_AlphaMode", 4)]
    [InlineData("m_RenderFace", 3)]
    public void Create_InvalidTargetEnum_IsRejected(string fieldName, int value)
    {
        Assert.Throws<InvalidDataException>(() => ShaderGraphTargetSet.Create(Parse(
            Graph("target"),
            Target(UniversalTarget, "target", "sub", $"\"{fieldName}\":{value}"),
            SubTarget(UniversalUnlit, "sub"))));
    }

    [Fact]
    public void Create_InvalidSubTargetEnum_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => ShaderGraphTargetSet.Create(Parse(
            Graph("target"),
            Target(UniversalTarget, "target", "sub"),
            SubTarget(UniversalLit, "sub", "\"m_NormalDropOffSpace\":3"))));
    }

    [Fact]
    public void Create_WrongBooleanType_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => ShaderGraphTargetSet.Create(Parse(
            Graph("target"),
            Target(UniversalTarget, "target", "sub", "\"m_AlphaClip\":1"),
            SubTarget(UniversalUnlit, "sub"))));
    }

    [Fact]
    public void Create_MissingTargetData_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => ShaderGraphTargetSet.Create(Parse(
            Graph("target"),
            Target(HighDefinitionTarget, "target", "sub", "\"m_Datas\":[{\"m_Id\":\"missing\"}]"),
            SubTarget(HighDefinitionUnlit, "sub"))));
    }

    [Fact]
    public void Create_LegacyGraphRequiresUpgrade()
    {
        const string legacy = "{\"m_SerializableNodes\":[],\"m_SerializableEdges\":[]}";

        Assert.Throws<NotSupportedException>(() => ShaderGraphTargetSet.Create(MultiJsonAsset.Parse(legacy)));
    }

    private static MultiJsonAsset Parse(params string[] documents)
        => MultiJsonAsset.Parse(string.Join("\n\n", documents));

    private static string Graph(params string[] targetIds)
        => "{\"m_SGVersion\":3,\"m_Type\":\"UnityEditor.ShaderGraph.GraphData\"," +
           "\"m_ObjectId\":\"graph\",\"m_Nodes\":[],\"m_ActiveTargets\":[" +
           string.Join(",", targetIds.Select(id => $"{{\"m_Id\":\"{id}\"}}")) + "]}";

    private static string Target(
        string type,
        string id,
        string subTargetId,
        string? additional = null)
        => Document(type, id, $"\"m_ActiveSubTarget\":{{\"m_Id\":\"{subTargetId}\"}}", additional);

    private static string SubTarget(string type, string id, string? additional = null)
        => Document(type, id, additional);

    private static string Document(string type, string id, params string?[] fields)
    {
        string suffix = string.Concat(fields.Where(field => !string.IsNullOrWhiteSpace(field)).Select(field => "," + field));
        return $"{{\"m_SGVersion\":0,\"m_Type\":\"{type}\",\"m_ObjectId\":\"{id}\"{suffix}}}";
    }

    private const string UniversalTarget = "UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget";
    private const string BuiltInTarget = "UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInTarget";
    private const string HighDefinitionTarget = "UnityEditor.Rendering.HighDefinition.ShaderGraph.HDTarget";
    private const string UniversalLit = "UnityEditor.Rendering.Universal.ShaderGraph.UniversalLitSubTarget";
    private const string UniversalUnlit = "UnityEditor.Rendering.Universal.ShaderGraph.UniversalUnlitSubTarget";
    private const string UniversalDecal = "UnityEditor.Rendering.Universal.ShaderGraph.UniversalDecalSubTarget";
    private const string UniversalFullscreen = "UnityEditor.Rendering.Universal.ShaderGraph.UniversalFullscreenSubTarget";
    private const string BuiltInLit = "UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInLitSubTarget";
    private const string BuiltInUnlit = "UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInUnlitSubTarget";
    private const string HighDefinitionLit = "UnityEditor.Rendering.HighDefinition.ShaderGraph.HDLitSubTarget";
    private const string HighDefinitionUnlit = "UnityEditor.Rendering.HighDefinition.ShaderGraph.HDUnlitSubTarget";
}
