using UnityEditor.ShaderGraph.Model;
using UnityEditor.ShaderGraph.Serialization;
using Xunit;

namespace Unity.ShaderGraph.Editor.Tests;

public sealed class ShaderGraphBlackboardTests
{
    [Fact]
    public void Create_EmptyGraph_ReturnsEmptyCollections()
    {
        ShaderGraphBlackboard blackboard = Create();

        Assert.Empty(blackboard.Properties);
        Assert.Empty(blackboard.Keywords);
        Assert.Empty(blackboard.Dropdowns);
        Assert.Empty(blackboard.Categories);
    }

    [Fact]
    public void Create_Vector1Property_ReadsUnityRangeAndReferenceOverride()
    {
        string property = Property(
            "p", "Vector1ShaderProperty", "Speed", "_Speed", "CUSTOM_SPEED", "2.5",
            ",\"m_FloatType\":1,\"m_RangeValues\":{\"x\":-2.0,\"y\":8.0}");

        ShaderGraphProperty value = Assert.Single(Create(properties: new[] { property }).Properties);

        Assert.Equal(ShaderGraphPropertyKind.Vector1, value.Kind);
        Assert.Equal("CUSTOM_SPEED", value.ReferenceName);
        Assert.Equal(1, value.FloatType);
        Assert.Equal(-2, value.RangeMinimum);
        Assert.Equal(8, value.RangeMaximum);
        Assert.Equal(2.5, value.Value.GetDouble());
    }

    [Theory]
    [InlineData("Vector2ShaderProperty", 1)]
    [InlineData("Vector3ShaderProperty", 2)]
    [InlineData("Vector4ShaderProperty", 3)]
    [InlineData("ColorShaderProperty", 4)]
    [InlineData("Texture2DShaderProperty", 6)]
    [InlineData("Texture2DArrayShaderProperty", 7)]
    [InlineData("Texture3DShaderProperty", 8)]
    [InlineData("CubemapShaderProperty", 9)]
    [InlineData("GradientShaderProperty", 10)]
    [InlineData("Matrix2ShaderProperty", 11)]
    [InlineData("Matrix3ShaderProperty", 12)]
    [InlineData("Matrix4ShaderProperty", 13)]
    [InlineData("SamplerStateShaderProperty", 14)]
    [InlineData("VirtualTextureShaderProperty", 15)]
    public void Create_AllUnity14PropertyKinds_AreTyped(string type, int expected)
    {
        string property = Property("p", type, "Value", "_Value", string.Empty, "{}");

        Assert.Equal((ShaderGraphPropertyKind)expected, Assert.Single(Create(properties: new[] { property }).Properties).Kind);
    }

    [Fact]
    public void Create_BooleanProperty_PreservesBooleanValueAndFlags()
    {
        string property = Property("p", "BooleanShaderProperty", "Enabled", "_Enabled", string.Empty, "true",
            ",\"m_GeneratePropertyBlock\":false,\"m_Hidden\":true,\"m_Precision\":1");

        ShaderGraphProperty value = Assert.Single(Create(properties: new[] { property }).Properties);

        Assert.Equal(ShaderGraphPropertyKind.Boolean, value.Kind);
        Assert.True(value.Value.GetBoolean());
        Assert.False(value.GeneratePropertyBlock);
        Assert.True(value.Hidden);
        Assert.Equal(1, value.Precision);
    }

    [Fact]
    public void Create_UsesDefaultReferenceWhenOverrideIsEmpty()
    {
        string property = Property("p", "Vector2ShaderProperty", "UV", "_UV", string.Empty, "{}");

        Assert.Equal("_UV", Assert.Single(Create(properties: new[] { property }).Properties).ReferenceName);
    }

    [Fact]
    public void Create_Keyword_ReadsEnumEntriesAndVariantControls()
    {
        string keyword = "{\"m_SGVersion\":1,\"m_Type\":\"UnityEditor.ShaderGraph.ShaderKeyword\",\"m_ObjectId\":\"k\",\"m_Name\":\"Quality\",\"m_DefaultReferenceName\":\"QUALITY\",\"m_OverrideReferenceName\":\"MATERIAL_QUALITY\",\"m_KeywordType\":1,\"m_KeywordDefinition\":1,\"m_KeywordScope\":1,\"m_KeywordStages\":63,\"m_Value\":1,\"m_Entries\":[{\"id\":1,\"displayName\":\"High\",\"referenceName\":\"HIGH\"},{\"id\":2,\"displayName\":\"Low\",\"referenceName\":\"LOW\"}]}";

        ShaderGraphKeyword value = Assert.Single(Create(keywords: new[] { keyword }).Keywords);

        Assert.Equal("MATERIAL_QUALITY", value.ReferenceName);
        Assert.Equal(1, value.KeywordType);
        Assert.Equal(1, value.Definition);
        Assert.Equal(1, value.Scope);
        Assert.Equal(63, value.Stages);
        Assert.Equal(1, value.Value);
        Assert.Equal(new[] { "HIGH", "LOW" }, value.Entries.Select(entry => entry.ReferenceName));
    }

    [Fact]
    public void Create_Dropdown_PreservesEntryIdsAndSerializedValue()
    {
        string dropdown = "{\"m_SGVersion\":0,\"m_Type\":\"UnityEditor.ShaderGraph.ShaderDropdown\",\"m_ObjectId\":\"d\",\"m_Name\":\"Lighting\",\"m_DefaultReferenceName\":\"_Lighting\",\"m_OverrideReferenceName\":\"\",\"m_Value\":3,\"m_Entries\":[{\"id\":3,\"displayName\":\"PBR\"},{\"id\":7,\"displayName\":\"Cel\"}]}";

        ShaderGraphDropdown value = Assert.Single(Create(dropdowns: new[] { dropdown }).Dropdowns);

        Assert.Equal("_Lighting", value.ReferenceName);
        Assert.Equal(3, value.Value);
        Assert.Equal(new[] { 3, 7 }, value.Entries.Select(entry => entry.Id));
    }

    [Fact]
    public void Create_Category_PreservesBlackboardChildOrder()
    {
        string a = Property("a", "Vector2ShaderProperty", "A", "_A", string.Empty, "{}");
        string b = Property("b", "Vector2ShaderProperty", "B", "_B", string.Empty, "{}");
        string category = "{\"m_SGVersion\":0,\"m_Type\":\"UnityEditor.ShaderGraph.CategoryData\",\"m_ObjectId\":\"c\",\"m_Name\":\"Inputs\",\"m_ChildObjectList\":[{\"m_Id\":\"b\"},{\"m_Id\":\"a\"}]}";

        ShaderGraphCategory value = Assert.Single(Create(new[] { a, b }, categories: new[] { category }).Categories);

        Assert.Equal("Inputs", value.Name);
        Assert.Equal(new[] { "b", "a" }, value.ChildObjectIds);
    }

    [Fact]
    public void Create_MissingReferencedObject_IsRejected()
    {
        MultiJsonAsset asset = MultiJsonAsset.Parse(Graph(new[] { "missing" }, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()));

        Assert.Throws<InvalidDataException>(() => ShaderGraphBlackboard.Create(asset));
    }

    [Fact]
    public void Create_DuplicateGraphReference_IsRejected()
    {
        string property = Property("p", "Vector2ShaderProperty", "P", "_P", string.Empty, "{}");
        string source = Graph(new[] { "p", "p" }, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()) + "\n" + property;

        Assert.Throws<InvalidDataException>(() => ShaderGraphBlackboard.Create(MultiJsonAsset.Parse(source)));
    }

    [Fact]
    public void Create_UnknownPropertyType_IsRejectedWithoutFallbackStub()
    {
        string property = Property("p", "UnknownShaderProperty", "P", "_P", string.Empty, "{}");

        Assert.Throws<NotSupportedException>(() => Create(properties: new[] { property }));
    }

    [Fact]
    public void Create_DuplicateKeywordEntryId_IsRejected()
    {
        string keyword = "{\"m_SGVersion\":1,\"m_Type\":\"UnityEditor.ShaderGraph.ShaderKeyword\",\"m_ObjectId\":\"k\",\"m_Name\":\"K\",\"m_DefaultReferenceName\":\"K\",\"m_OverrideReferenceName\":\"\",\"m_KeywordType\":1,\"m_KeywordDefinition\":0,\"m_KeywordScope\":0,\"m_KeywordStages\":63,\"m_Value\":0,\"m_Entries\":[{\"id\":1,\"displayName\":\"A\",\"referenceName\":\"A\"},{\"id\":1,\"displayName\":\"B\",\"referenceName\":\"B\"}]}";

        Assert.Throws<InvalidDataException>(() => Create(keywords: new[] { keyword }));
    }

    [Fact]
    public void Create_CategoryUnknownChild_IsRejected()
    {
        string category = "{\"m_SGVersion\":0,\"m_Type\":\"UnityEditor.ShaderGraph.CategoryData\",\"m_ObjectId\":\"c\",\"m_Name\":\"\",\"m_ChildObjectList\":[{\"m_Id\":\"missing\"}]}";

        Assert.Throws<InvalidDataException>(() => Create(categories: new[] { category }));
    }

    [Fact]
    public void Create_LegacyGraph_RequiresUpgradeFirst()
    {
        const string legacy = "{\"m_SerializableNodes\":[],\"m_SerializableEdges\":[]}";

        Assert.Throws<NotSupportedException>(() => ShaderGraphBlackboard.Create(MultiJsonAsset.Parse(legacy)));
    }

    private static ShaderGraphBlackboard Create(
        string[]? properties = null,
        string[]? keywords = null,
        string[]? dropdowns = null,
        string[]? categories = null)
    {
        properties ??= Array.Empty<string>();
        keywords ??= Array.Empty<string>();
        dropdowns ??= Array.Empty<string>();
        categories ??= Array.Empty<string>();
        string source = Graph(
            ObjectIds(properties), ObjectIds(keywords), ObjectIds(dropdowns), ObjectIds(categories));
        foreach (string value in properties.Concat(keywords).Concat(dropdowns).Concat(categories)) source += "\n" + value;
        return ShaderGraphBlackboard.Create(MultiJsonAsset.Parse(source));
    }

    private static string[] ObjectIds(IEnumerable<string> documents)
        => documents.Select(document =>
                MultiJsonAsset.Parse(Graph(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()) + "\n" + document)
                    .Documents[1].ObjectId)
            .ToArray();

    private static string Graph(string[] properties, string[] keywords, string[] dropdowns, string[] categories)
        => "{\"m_SGVersion\":3,\"m_Type\":\"UnityEditor.ShaderGraph.GraphData\",\"m_ObjectId\":\"graph\",\"m_Properties\":" + References(properties) +
           ",\"m_Keywords\":" + References(keywords) + ",\"m_Dropdowns\":" + References(dropdowns) +
           ",\"m_CategoryData\":" + References(categories) + ",\"m_Nodes\":[],\"m_Edges\":[]}";

    private static string References(IEnumerable<string> ids)
        => "[" + string.Join(",", ids.Select(id => $"{{\"m_Id\":\"{id}\"}}")) + "]";

    private static string Property(
        string id,
        string type,
        string name,
        string defaultReference,
        string overrideReference,
        string valueJson,
        string extra = "")
        => "{\"m_SGVersion\":1,\"m_Type\":\"UnityEditor.ShaderGraph.Internal." + type + "\",\"m_ObjectId\":\"" + id +
           "\",\"m_Name\":\"" + name + "\",\"m_DefaultReferenceName\":\"" + defaultReference +
           "\",\"m_OverrideReferenceName\":\"" + overrideReference + "\",\"m_Value\":" + valueJson + extra + "}";
}
