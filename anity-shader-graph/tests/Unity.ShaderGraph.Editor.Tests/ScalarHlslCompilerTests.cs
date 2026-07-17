using System.Globalization;
using UnityEditor.ShaderGraph.Generation;
using UnityEditor.ShaderGraph.Serialization;
using Xunit;

namespace Unity.ShaderGraph.Editor.Tests;

public sealed class ScalarHlslCompilerTests
{
    [Fact]
    public void Compile_Vector1Node_EmitsFiniteFloatFunction()
    {
        string hlsl = Compile(new[] { Constant("value", 2.5) }, "value");

        Assert.Equal("float AnityShaderGraphEvaluate()\n{\n    float n0 = 2.5;\n    return n0;\n}\n", hlsl);
    }

    [Fact]
    public void Compile_IntegerConstant_UsesHlslFloatingLiteral()
    {
        Assert.Contains("float n0 = 2.0;", Compile(new[] { Constant("value", 2) }, "value"), StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_AddNode_UsesConnectedInputsInDependencyOrder()
    {
        string hlsl = Compile(
            new[] { Constant("a", 2), Constant("b", 3), Binary("sum", "AddNode", 9, 8) },
            "sum",
            Edge("a", 1, "sum", 0), Edge("b", 1, "sum", 1));

        Assert.Contains("float n0 = 2.0;\n    float n1 = 3.0;\n    float n2 = (n0 + n1);", hlsl, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_UnconnectedInputs_UseSerializedSlotDefaults()
    {
        string hlsl = Compile(new[] { Binary("sum", "AddNode", 4.25, -1.5) }, "sum");

        Assert.Contains("float n0 = (4.25 + -1.5);", hlsl, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SubtractNode", "-")]
    [InlineData("MultiplyNode", "*")]
    [InlineData("DivideNode", "/")]
    public void Compile_BinaryArithmeticNodes_EmitExpectedOperator(string nodeType, string operation)
    {
        string hlsl = Compile(new[] { Binary("node", nodeType, 6, 2) }, "node");

        Assert.Contains($"(6.0 {operation} 2.0)", hlsl, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_DiamondDependencies_EmitSharedNodeOnce()
    {
        string hlsl = Compile(
            new[]
            {
                Constant("root", 2), Binary("left", "MultiplyNode", 0, 3),
                Binary("right", "AddNode", 0, 4), Binary("output", "AddNode", 0, 0)
            },
            "output",
            Edge("root", 1, "left", 0), Edge("root", 1, "right", 0),
            Edge("left", 2, "output", 0), Edge("right", 2, "output", 1));

        Assert.Equal(1, Count(hlsl, " = 2.0;"));
        Assert.Equal(4, Count(hlsl, "    float n"));
    }

    [Fact]
    public void Compile_IsDeterministicAcrossRepeatedCalls()
    {
        MultiJsonAsset asset = BuildAsset(new[] { Constant("a", 1), Binary("sum", "AddNode", 0, 2) }, Edge("a", 1, "sum", 0));

        Assert.Equal(ScalarHlslCompiler.Compile(asset, "sum"), ScalarHlslCompiler.Compile(asset, "sum"));
    }

    [Fact]
    public void Compile_UsesInvariantCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Contains("1.5", Compile(new[] { Constant("value", 1.5) }, "value"), StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Compile_UnsupportedNodeType_FailsInsteadOfGeneratingPlaceholder()
    {
        string unsupported = Node("noise", "SimpleNoiseNode", Array.Empty<string>(), null);

        NotSupportedException exception = Assert.Throws<NotSupportedException>(
            () => Compile(new[] { unsupported }, "noise"));
        Assert.Contains("SimpleNoiseNode", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_MissingOutputNode_IsRejected()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => Compile(new[] { Constant("value", 1) }, "missing"));
        Assert.Contains("missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_BinaryNodeWithMissingSlot_IsRejected()
    {
        string slot = Slot("only", 0, 0, 1);
        string node = Node("sum", "AddNode", new[] { "only" }, null);

        Assert.Throws<InvalidDataException>(() => Compile(new[] { node, slot }, "sum"));
    }

    [Fact]
    public void Compile_NonScalarDefault_IsRejected()
    {
        string slotA = Slot("a", 0, 0, "{\"x\":1.0,\"y\":2.0}", true);
        string slotB = Slot("b", 1, 0, 2);
        string slotOut = Slot("out", 2, 1, 0);
        string node = Node("sum", "AddNode", new[] { "a", "b", "out" }, null);

        Assert.Throws<InvalidDataException>(() => Compile(new[] { node, slotA, slotB, slotOut }, "sum"));
    }

    [Fact]
    public void Compile_CyclicGraph_IsRejectedBeforeGeneration()
    {
        MultiJsonAsset asset = BuildAsset(
            new[] { Binary("a", "AddNode", 0, 1), Binary("b", "AddNode", 0, 1) },
            Edge("a", 2, "b", 0), Edge("b", 2, "a", 0));

        Assert.Throws<InvalidDataException>(() => ScalarHlslCompiler.Compile(asset, "a"));
    }

    [Fact]
    public void Compile_NullAndEmptyOutputArguments_AreRejected()
    {
        MultiJsonAsset asset = BuildAsset(new[] { Constant("value", 1) });

        Assert.Throws<ArgumentNullException>(() => ScalarHlslCompiler.Compile(null!, "value"));
        Assert.Throws<ArgumentException>(() => ScalarHlslCompiler.Compile(asset, " "));
    }

    [Fact]
    public void Compile_PropertyNode_DefaultDeclarationUsesUnityPerMaterialCbuffer()
    {
        string hlsl = CompileProperty("Vector1ShaderProperty", generatePropertyBlock: true);

        Assert.StartsWith("CBUFFER_START(UnityPerMaterial)\n    float _Speed;\nCBUFFER_END", hlsl, StringComparison.Ordinal);
        Assert.Contains("float n0 = _Speed;", hlsl, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_PropertyNode_NonExposedDeclarationUsesGlobalScope()
    {
        string hlsl = CompileProperty("Vector1ShaderProperty", generatePropertyBlock: false);

        Assert.Contains("// Object and Global properties\nfloat _Speed;", hlsl, StringComparison.Ordinal);
        Assert.DoesNotContain("    float _Speed;", hlsl, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_PropertyNode_DoNotDeclareUsesExternalReferenceWithoutDeclaration()
    {
        string hlsl = CompileProperty("Vector1ShaderProperty", true, overrideDeclaration: true, declaration: 0);

        Assert.Contains("float n0 = _Speed;", hlsl, StringComparison.Ordinal);
        Assert.Equal(1, Count(hlsl, "_Speed"));
    }

    [Fact]
    public void Compile_PropertyNode_HybridDeclarationEmitsDotsAndClassicInstancingPaths()
    {
        string hlsl = CompileProperty("Vector1ShaderProperty", true, overrideDeclaration: true, declaration: 3);

        Assert.Contains("UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float, _Speed)", hlsl, StringComparison.Ordinal);
        Assert.Contains("UNITY_DEFINE_INSTANCED_PROP(float, _Speed)", hlsl, StringComparison.Ordinal);
        Assert.Contains("UNITY_ACCESS_HYBRID_INSTANCED_PROP(_Speed, float)", hlsl, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_PropertyNode_BooleanUsesFloatStorageLikeUnity14()
    {
        string hlsl = CompileProperty("BooleanShaderProperty", true);

        Assert.Contains("float _Speed;", hlsl, StringComparison.Ordinal);
        Assert.Contains("float n0 = _Speed;", hlsl, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_PropertyNode_OverrideReferenceNameIsUsedByDeclarationAndExpression()
    {
        string hlsl = CompileProperty("Vector1ShaderProperty", true, overrideReference: "CUSTOM_SPEED");

        Assert.Equal(2, Count(hlsl, "CUSTOM_SPEED"));
        Assert.DoesNotContain("_Speed", hlsl, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_PropertyNode_MissingPropertyReferenceIsRejected()
    {
        string source = PropertyGraph(Array.Empty<string>(), new[] { "node" }) + "\n" + PropertyNode("node", "missing");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => ScalarHlslCompiler.Compile(MultiJsonAsset.Parse(source), "node"));
        Assert.Contains("missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_PropertyNode_MissingMPropertyFieldIsRejected()
    {
        string node = Node("node", "PropertyNode", Array.Empty<string>(), null);
        string source = PropertyGraph(Array.Empty<string>(), new[] { "node" }) + "\n" + node;

        Assert.Throws<InvalidDataException>(() => ScalarHlslCompiler.Compile(MultiJsonAsset.Parse(source), "node"));
    }

    [Fact]
    public void Compile_PropertyNode_VectorPropertyIsExplicitlyUnsupportedByScalarCompiler()
    {
        Assert.Throws<NotSupportedException>(() => CompileProperty("Vector2ShaderProperty", true));
    }

    [Fact]
    public void Compile_TwoPropertyNodesSharingOneProperty_DeclareItOnce()
    {
        string property = ShaderProperty("Vector1ShaderProperty", true, false, 0, string.Empty);
        string binary = Binary("sum", "AddNode", 0, 0);
        string source = PropertyGraph(new[] { "p" }, new[] { "left", "right", "sum" },
            Edge("left", 0, "sum", 0), Edge("right", 0, "sum", 1)) + "\n" + property + "\n" +
            PropertyNode("left", "p") + "\n" + PropertyNode("right", "p") + "\n" + binary;

        string hlsl = ScalarHlslCompiler.Compile(MultiJsonAsset.Parse(source), "sum");

        Assert.Equal(3, Count(hlsl, "_Speed"));
        Assert.Equal(1, Count(hlsl, "    float _Speed;"));
    }

    private static string Compile(string[] objects, string outputNode, params string[] edges)
        => ScalarHlslCompiler.Compile(BuildAsset(objects, edges), outputNode);

    private static string CompileProperty(
        string propertyType,
        bool generatePropertyBlock,
        bool overrideDeclaration = false,
        int declaration = 0,
        string overrideReference = "")
    {
        string source = PropertyGraph(new[] { "p" }, new[] { "node" }) + "\n" +
                        ShaderProperty(propertyType, generatePropertyBlock, overrideDeclaration, declaration, overrideReference) + "\n" +
                        PropertyNode("node", "p");
        return ScalarHlslCompiler.Compile(MultiJsonAsset.Parse(source), "node");
    }

    private static string PropertyGraph(string[] propertyIds, string[] nodeIds, params string[] edges)
        => "{\"m_SGVersion\":3,\"m_Type\":\"UnityEditor.ShaderGraph.GraphData\",\"m_ObjectId\":\"graph\",\"m_Properties\":[" +
           string.Join(",", propertyIds.Select(id => $"{{\"m_Id\":\"{id}\"}}")) +
           "],\"m_Keywords\":[],\"m_Dropdowns\":[],\"m_CategoryData\":[],\"m_Nodes\":[" +
           string.Join(",", nodeIds.Select(id => $"{{\"m_Id\":\"{id}\"}}")) + "],\"m_Edges\":[" +
           string.Join(",", edges) + "]}";

    private static string ShaderProperty(
        string type,
        bool generatePropertyBlock,
        bool overrideDeclaration,
        int declaration,
        string overrideReference)
    {
        string value = type == "BooleanShaderProperty" ? "true" : type == "Vector1ShaderProperty" ? "1.0" : "{}";
        string vector1 = type == "Vector1ShaderProperty" ? ",\"m_FloatType\":0,\"m_RangeValues\":{\"x\":0.0,\"y\":1.0}" : string.Empty;
        return "{\"m_SGVersion\":1,\"m_Type\":\"UnityEditor.ShaderGraph.Internal." + type +
               "\",\"m_ObjectId\":\"p\",\"m_Name\":\"Speed\",\"m_DefaultReferenceName\":\"_Speed\",\"m_OverrideReferenceName\":\"" +
               overrideReference + "\",\"m_GeneratePropertyBlock\":" + generatePropertyBlock.ToString().ToLowerInvariant() +
               ",\"overrideHLSLDeclaration\":" + overrideDeclaration.ToString().ToLowerInvariant() +
               ",\"hlslDeclarationOverride\":" + declaration + ",\"m_Value\":" + value + vector1 + "}";
    }

    private static string PropertyNode(string nodeId, string propertyId)
        => "{\"m_SGVersion\":0,\"m_Type\":\"UnityEditor.ShaderGraph.PropertyNode\",\"m_ObjectId\":\"" + nodeId +
           "\",\"m_Slots\":[],\"m_Property\":{\"m_Id\":\"" + propertyId + "\"}}";

    private static MultiJsonAsset BuildAsset(string[] objects, params string[] edges)
    {
        string[] nodeIds = objects
            .Where(value => value.Contains("Node\"", StringComparison.Ordinal))
            .Select(value => MultiJsonAsset.Parse(Graph(Array.Empty<string>(), Array.Empty<string>()) + "\n" + value).Documents[1].ObjectId)
            .ToArray();
        string source = Graph(nodeIds, edges);
        foreach (string value in objects) source += "\n" + value;
        return MultiJsonAsset.Parse(source);
    }

    private static string Graph(string[] nodeIds, string[] edges)
        => "{\"m_SGVersion\":3,\"m_Type\":\"UnityEditor.ShaderGraph.GraphData\",\"m_ObjectId\":\"graph\",\"m_Nodes\":[" +
           string.Join(",", nodeIds.Select(id => $"{{\"m_Id\":\"{id}\"}}")) + "],\"m_Edges\":[" +
           string.Join(",", edges) + "]}";

    private static string Constant(string id, double value)
    {
        string input = id + "-in";
        string output = id + "-out";
        return Node(id, "Vector1Node", new[] { input, output }, value) + "\n" +
               Slot(input, 0, 0, value) + "\n" + Slot(output, 1, 1, value);
    }

    private static string Binary(string id, string nodeType, double left, double right)
    {
        string a = id + "-a";
        string b = id + "-b";
        string output = id + "-out";
        return Node(id, nodeType, new[] { a, b, output }, null) + "\n" +
               Slot(a, 0, 0, left) + "\n" + Slot(b, 1, 0, right) + "\n" + Slot(output, 2, 1, 0);
    }

    private static string Node(string id, string nodeType, string[] slots, double? value)
        => "{\"m_SGVersion\":0,\"m_Type\":\"UnityEditor.ShaderGraph." + nodeType + "\",\"m_ObjectId\":\"" + id +
           "\",\"m_Slots\":[" + string.Join(",", slots.Select(slot => $"{{\"m_Id\":\"{slot}\"}}")) + "]" +
           (value.HasValue ? ",\"m_Value\":" + value.Value.ToString("R", CultureInfo.InvariantCulture) : string.Empty) + "}";

    private static string Slot(string objectId, int slotId, int slotType, double value)
        => Slot(objectId, slotId, slotType, value.ToString("R", CultureInfo.InvariantCulture), true);

    private static string Slot(string objectId, int slotId, int slotType, string valueJson, bool raw)
        => "{\"m_SGVersion\":0,\"m_Type\":\"UnityEditor.ShaderGraph.Vector1MaterialSlot\",\"m_ObjectId\":\"" + objectId +
           "\",\"m_Id\":" + slotId.ToString(CultureInfo.InvariantCulture) + ",\"m_SlotType\":" +
           slotType.ToString(CultureInfo.InvariantCulture) + ",\"m_Value\":" + (raw ? valueJson : "\"" + valueJson + "\"") + "}";

    private static string Edge(string outputNode, int outputSlot, string inputNode, int inputSlot)
        => $"{{\"m_OutputSlot\":{{\"m_Node\":{{\"m_Id\":\"{outputNode}\"}},\"m_SlotId\":{outputSlot}}},\"m_InputSlot\":{{\"m_Node\":{{\"m_Id\":\"{inputNode}\"}},\"m_SlotId\":{inputSlot}}}}}";

    private static int Count(string value, string pattern)
    {
        int count = 0;
        for (int index = 0; (index = value.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0; index += pattern.Length) count++;
        return count;
    }
}
