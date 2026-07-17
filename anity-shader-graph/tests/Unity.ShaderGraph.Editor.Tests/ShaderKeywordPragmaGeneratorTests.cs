using UnityEditor.ShaderGraph.Generation;
using UnityEditor.ShaderGraph.Model;
using UnityEditor.ShaderGraph.Serialization;
using Xunit;

namespace Unity.ShaderGraph.Editor.Tests;

public sealed class ShaderKeywordPragmaGeneratorTests
{
    [Fact]
    public void Generate_BooleanLocalShaderFeature_MatchesUnity14Syntax()
    {
        Assert.Equal(
            "#pragma shader_feature_local _ FEATURE_ON\n",
            Generate(Keyword("FEATURE_ON", type: 0, definition: 0, scope: 0, stages: 63, value: 0)));
    }

    [Fact]
    public void Generate_BooleanGlobalMultiCompile_MatchesUnity14Syntax()
    {
        Assert.Equal(
            "#pragma multi_compile _ FEATURE_ON\n",
            Generate(Keyword("FEATURE_ON", type: 0, definition: 1, scope: 1, stages: 0, value: 1)));
    }

    [Fact]
    public void Generate_EnumVariants_PrefixEntryReferenceNames()
    {
        string keyword = Keyword(
            "QUALITY", 1, 1, 1, 63, 1,
            "{\"id\":1,\"displayName\":\"High\",\"referenceName\":\"HIGH\"}",
            "{\"id\":2,\"displayName\":\"Low\",\"referenceName\":\"LOW\"}");

        Assert.Equal("#pragma multi_compile QUALITY_HIGH QUALITY_LOW\n", Generate(keyword));
    }

    [Fact]
    public void Generate_PredefinedKeyword_EmitsNoPragma()
    {
        Assert.Empty(Generate(Keyword("BUILTIN", 0, 2, 1, 63, 0)));
    }

    [Theory]
    [InlineData(1, "_vertex")]
    [InlineData(2, "_fragment")]
    [InlineData(4, "_geometry")]
    [InlineData(8, "_hull")]
    [InlineData(16, "_domain")]
    [InlineData(32, "_raytracing")]
    public void Generate_SingleStage_AppendsUnityStageSuffix(int stages, string suffix)
    {
        Assert.Equal(
            $"#pragma shader_feature_local{suffix} _ FEATURE\n",
            Generate(Keyword("FEATURE", 0, 0, 0, stages, 0)));
    }

    [Fact]
    public void Generate_MultipleStages_EmitsOnePragmaPerStageInUnityOrder()
    {
        Assert.Equal(
            "#pragma shader_feature_vertex _ FEATURE\n#pragma shader_feature_fragment _ FEATURE\n#pragma shader_feature_domain _ FEATURE\n",
            Generate(Keyword("FEATURE", 0, 0, 1, 1 | 2 | 16, 0)));
    }

    [Fact]
    public void Generate_MultipleKeywords_PreservesBlackboardOrder()
    {
        ShaderGraphBlackboard blackboard = CreateBlackboard(
            Keyword("FIRST", 0, 0, 1, 63, 0),
            Keyword("SECOND", 0, 1, 1, 63, 0));

        Assert.Equal(
            "#pragma shader_feature _ FIRST\n#pragma multi_compile _ SECOND\n",
            ShaderKeywordPragmaGenerator.Generate(blackboard));
    }

    [Fact]
    public void Generate_NullBlackboard_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => ShaderKeywordPragmaGenerator.Generate(null!));
    }

    [Theory]
    [InlineData(2, 0, 0, 63, 0)]
    [InlineData(0, 3, 0, 63, 0)]
    [InlineData(0, 0, 2, 63, 0)]
    [InlineData(0, 0, 0, 64, 0)]
    [InlineData(0, 0, 0, 63, 2)]
    public void Create_InvalidKeywordEnumsAndMasks_AreRejected(
        int type, int definition, int scope, int stages, int value)
    {
        string keyword = Keyword("INVALID", type, definition, scope, stages, value);

        Assert.Throws<InvalidDataException>(() => CreateBlackboard(keyword));
    }

    private static string Generate(string keyword)
        => ShaderKeywordPragmaGenerator.Generate(CreateBlackboard(keyword));

    private static ShaderGraphBlackboard CreateBlackboard(params string[] keywords)
    {
        string refs = string.Join(",", keywords.Select((_, index) => $"{{\"m_Id\":\"k{index}\"}}"));
        string graph = "{\"m_SGVersion\":3,\"m_Type\":\"UnityEditor.ShaderGraph.GraphData\",\"m_ObjectId\":\"graph\",\"m_Properties\":[],\"m_Keywords\":[" + refs + "],\"m_Dropdowns\":[],\"m_CategoryData\":[],\"m_Nodes\":[],\"m_Edges\":[]}";
        string source = graph;
        for (int index = 0; index < keywords.Length; index++)
            source += "\n" + keywords[index].Replace("\"m_ObjectId\":\"k\"", $"\"m_ObjectId\":\"k{index}\"", StringComparison.Ordinal);
        return ShaderGraphBlackboard.Create(MultiJsonAsset.Parse(source));
    }

    private static string Keyword(
        string referenceName,
        int type,
        int definition,
        int scope,
        int stages,
        int value,
        params string[] entries)
        => "{\"m_SGVersion\":1,\"m_Type\":\"UnityEditor.ShaderGraph.ShaderKeyword\",\"m_ObjectId\":\"k\",\"m_Name\":\"Keyword\",\"m_DefaultReferenceName\":\"" + referenceName +
           "\",\"m_OverrideReferenceName\":\"\",\"m_KeywordType\":" + type + ",\"m_KeywordDefinition\":" + definition +
           ",\"m_KeywordScope\":" + scope + ",\"m_KeywordStages\":" + stages + ",\"m_Value\":" + value +
           ",\"m_Entries\":[" + string.Join(",", entries) + "]}";
}
