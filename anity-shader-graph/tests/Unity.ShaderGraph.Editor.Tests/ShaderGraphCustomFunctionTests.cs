using UnityEditor.ShaderGraph.Model;
using UnityEditor.ShaderGraph.Serialization;
using Xunit;

namespace Unity.ShaderGraph.Editor.Tests;

public sealed class ShaderGraphCustomFunctionTests
{
    [Fact]
    public void Create_StringFunction_PreservesSourceAndPrecisionName()
    {
        ShaderGraphCustomFunction function = Parse(Function("node", 1, "Remap", string.Empty, "Out = In;", "in", "out"),
            Slot("in", "Vector1MaterialSlot", 0, 0, "In"),
            Slot("out", "Vector1MaterialSlot", 1, 1, "Out")).Functions.Single();

        Assert.Equal(ShaderGraphHlslSourceType.String, function.SourceType);
        Assert.Equal("Remap_$precision", function.HlslFunctionName);
        Assert.Equal("Out = In;", function.FunctionBody);
        Assert.True(function.IsConfigured);
    }

    [Fact]
    public void Create_FileFunction_PreservesGuidAndConfiguration()
    {
        ShaderGraphCustomFunction function = Parse(Function("node", 0, "Remap", "0123456789abcdef", "ignored", "out"),
            Slot("out", "Vector1MaterialSlot", 1, 1, "Out")).Functions.Single();

        Assert.Equal(ShaderGraphHlslSourceType.File, function.SourceType);
        Assert.Equal("0123456789abcdef", function.FunctionSource);
        Assert.True(function.IsConfigured);
    }

    [Fact]
    public void Create_PreservesSerializedSlotAndDirectionOrder()
    {
        ShaderGraphCustomFunction function = Parse(Function("node", 1, "F", string.Empty, "C = A + B;", "b", "out", "a"),
            Slot("a", "Vector1MaterialSlot", 3, 0, "A"),
            Slot("b", "Vector1MaterialSlot", 7, 0, "B"),
            Slot("out", "Vector1MaterialSlot", 9, 1, "C")).Functions.Single();

        Assert.Equal(new[] { "B", "C", "A" }, function.Slots.Select(slot => slot.ShaderOutputName));
        Assert.Equal(new[] { "B", "A" }, function.Inputs.Select(slot => slot.ShaderOutputName));
        Assert.Equal("C", Assert.Single(function.Outputs).ShaderOutputName);
    }

    [Theory]
    [InlineData("Vector1MaterialSlot", "Vector1")]
    [InlineData("Vector2MaterialSlot", "Vector2")]
    [InlineData("Vector3MaterialSlot", "Vector3")]
    [InlineData("Vector4MaterialSlot", "Vector4")]
    [InlineData("BooleanMaterialSlot", "Boolean")]
    [InlineData("Texture2DInputMaterialSlot", "Texture2DInput")]
    public void Create_MapsEveryObservedUnity14SlotType(string type, string expected)
    {
        ShaderGraphCustomFunction function = Parse(Function("node", 1, "F", string.Empty, "Out = 0;", "slot"),
            Slot("slot", type, 5, 0, "Input")).Functions.Single();

        Assert.Equal(expected, Assert.Single(function.Slots).Kind.ToString());
    }

    [Theory]
    [InlineData("Enter function name here...", "Out = 1;")]
    [InlineData("F", "Enter function body here...")]
    [InlineData("F", "")]
    public void Create_StringPlaceholder_IsPreservedButNotConfigured(string name, string body)
    {
        ShaderGraphCustomFunction function = Parse(Function("node", 1, name, string.Empty, body)).Functions.Single();

        Assert.False(function.IsConfigured);
        Assert.Empty(function.Outputs);
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(0, "Enter function source file path here...")]
    public void Create_FilePlaceholder_IsNotConfigured(int sourceType, string source)
    {
        ShaderGraphCustomFunction function = Parse(Function("node", sourceType, "F", source, "ignored")).Functions.Single();

        Assert.False(function.IsConfigured);
    }

    [Fact]
    public void Create_InvalidSourceEnum_IsRejected()
        => Assert.Throws<InvalidDataException>(() => Parse(Function("node", 2, "F", string.Empty, "Out = 1;")));

    [Fact]
    public void Create_DuplicateSlotReference_IsRejected()
        => Assert.Throws<InvalidDataException>(() => Parse(
            Function("node", 1, "F", string.Empty, "Out = 1;", "slot", "slot"),
            Slot("slot", "Vector1MaterialSlot", 0, 1, "Out")));

    [Fact]
    public void Create_MissingSlotReference_IsRejected()
        => Assert.Throws<InvalidDataException>(() => Parse(Function("node", 1, "F", string.Empty, "Out = 1;", "missing")));

    [Fact]
    public void Create_InvalidSlotDirection_IsRejected()
        => Assert.Throws<InvalidDataException>(() => Parse(
            Function("node", 1, "F", string.Empty, "Out = 1;", "slot"),
            Slot("slot", "Vector1MaterialSlot", 0, 2, "Out")));

    [Fact]
    public void Create_TextureOutput_IsRejected()
        => Assert.Throws<InvalidDataException>(() => Parse(
            Function("node", 1, "F", string.Empty, "Out = 1;", "slot"),
            Slot("slot", "Texture2DInputMaterialSlot", 0, 1, "Out")));

    [Fact]
    public void Create_UnsupportedSlotType_IsRejected()
        => Assert.Throws<NotSupportedException>(() => Parse(
            Function("node", 1, "F", string.Empty, "Out = 1;", "slot"),
            Slot("slot", "Matrix4MaterialSlot", 0, 0, "Matrix")));

    [Fact]
    public void Create_LegacyAsset_RequiresUpgrade()
    {
        MultiJsonAsset legacy = MultiJsonAsset.Parse("{\"m_SerializableNodes\":[],\"m_SerializableEdges\":[]}");

        Assert.Throws<NotSupportedException>(() => ShaderGraphCustomFunctionSet.Create(legacy));
    }

    private static ShaderGraphCustomFunctionSet Parse(string function, params string[] slots)
    {
        string source = "{\"m_SGVersion\":3,\"m_Type\":\"UnityEditor.ShaderGraph.GraphData\",\"m_ObjectId\":\"graph\",\"m_Nodes\":[{\"m_Id\":\"node\"}],\"m_Edges\":[]}" +
                        "\n" + function;
        foreach (string slot in slots) source += "\n" + slot;
        return ShaderGraphCustomFunctionSet.Create(MultiJsonAsset.Parse(source));
    }

    private static string Function(
        string id,
        int sourceType,
        string name,
        string source,
        string body,
        params string[] slots)
        => "{\"m_SGVersion\":0,\"m_Type\":\"UnityEditor.ShaderGraph.CustomFunctionNode\",\"m_ObjectId\":\"" + id +
           "\",\"m_SourceType\":" + sourceType + ",\"m_FunctionName\":\"" + Escape(name) +
           "\",\"m_FunctionSource\":\"" + Escape(source) + "\",\"m_FunctionBody\":\"" + Escape(body) +
           "\",\"m_Slots\":[" + string.Join(",", slots.Select(slot => "{\"m_Id\":\"" + slot + "\"}")) + "]}";

    private static string Slot(string id, string type, int slotId, int direction, string shaderName)
        => "{\"m_SGVersion\":0,\"m_Type\":\"UnityEditor.ShaderGraph." + type + "\",\"m_ObjectId\":\"" + id +
           "\",\"m_Id\":" + slotId + ",\"m_SlotType\":" + direction + ",\"m_DisplayName\":\"" + shaderName +
           "\",\"m_ShaderOutputName\":\"" + shaderName + "\",\"m_Value\":0.0}";

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
}
