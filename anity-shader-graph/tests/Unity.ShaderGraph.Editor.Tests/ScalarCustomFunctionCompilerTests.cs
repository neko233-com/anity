using System.Globalization;
using UnityEditor.ShaderGraph.Generation;
using UnityEditor.ShaderGraph.Serialization;
using Xunit;

namespace Unity.ShaderGraph.Editor.Tests;

public sealed class ScalarCustomFunctionCompilerTests
{
    [Fact]
    public void Compile_StringFunction_EmitsDefinitionAndInvocation()
    {
        string hlsl = Compile(
            new[] { Custom("custom", "Remap", "Out = In * 2.0;", "custom-in", "custom-out") },
            "custom",
            Slot("custom-in", 0, 0, 3.5), Slot("custom-out", 1, 1, 0));

        Assert.StartsWith("void Remap_float(float In, out float Out)\n{\n    Out = In * 2.0;\n}\n\n", hlsl, StringComparison.Ordinal);
        Assert.Contains("float n0;\n    Remap_float(3.5, n0);\n    return n0;", hlsl, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_ConnectedInput_EmitsDependencyBeforeCall()
    {
        string hlsl = Compile(
            new[] { Constant("value", 4), Custom("custom", "Remap", "Out = In;", "custom-in", "custom-out") },
            "custom",
            new[] { Slot("custom-in", 0, 0, 0), Slot("custom-out", 1, 1, 0) },
            Edge("value", 1, "custom", 0));

        Assert.Contains("float n0 = 4.0;\n    float n1;\n    Remap_float(n0, n1);", hlsl, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_FileFunction_ResolvesGuidAndEmitsInclude()
    {
        string hlsl = Compile(
            new[] { CustomFile("custom", "Remap", "guid-1", "custom-out") },
            "custom",
            new[] { Slot("custom-out", 1, 1, 0) },
            includePathResolver: guid => guid == "guid-1" ? "Assets/Shaders/Remap.hlsl" : string.Empty);

        Assert.StartsWith("#include \"Assets/Shaders/Remap.hlsl\"\n\n", hlsl, StringComparison.Ordinal);
        Assert.Contains("Remap_float(n0);", hlsl, StringComparison.Ordinal);
        Assert.DoesNotContain("void Remap_float", hlsl, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_FileFunction_NormalizesWindowsSeparators()
    {
        string hlsl = Compile(
            new[] { CustomFile("custom", "F", "guid", "out") }, "custom",
            new[] { Slot("out", 0, 1, 0) }, includePathResolver: _ => "Assets\\Shaders\\F.cginc");

        Assert.StartsWith("#include \"Assets/Shaders/F.cginc\"", hlsl, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_EquivalentFunctions_EmitOneDefinition()
    {
        string hlsl = CompilePair("Out = In;", "Out = In;");

        Assert.Equal(1, Count(hlsl, "void Shared_float"));
        Assert.Equal(2, Count(hlsl, "    Shared_float("));
    }

    [Fact]
    public void Compile_ConflictingFunctionBodies_AreRejected()
        => Assert.Throws<InvalidDataException>(() => CompilePair("Out = In;", "Out = In * 2.0;"));

    [Fact]
    public void Compile_FileFunctionWithoutResolver_IsRejected()
        => Assert.Throws<InvalidDataException>(() => Compile(
            new[] { CustomFile("custom", "F", "guid", "out") }, "custom", Slot("out", 0, 1, 0)));

    [Theory]
    [InlineData("Assets/F.txt")]
    [InlineData("Assets/F.shader")]
    [InlineData("Assets/F.hlsl\nInjected")]
    [InlineData("Assets/F\".hlsl")]
    public void Compile_InvalidFilePath_IsRejected(string path)
        => Assert.Throws<InvalidDataException>(() => Compile(
            new[] { CustomFile("custom", "F", "guid", "out") }, "custom",
            new[] { Slot("out", 0, 1, 0) }, includePathResolver: _ => path));

    [Theory]
    [InlineData("1Function")]
    [InlineData("Has Space")]
    [InlineData("Has-Dash")]
    public void Compile_InvalidHlslFunctionName_IsRejected(string name)
        => Assert.Throws<InvalidDataException>(() => Compile(
            new[] { Custom("custom", name, "Out = 1;", "out") }, "custom", Slot("out", 0, 1, 0)));

    [Fact]
    public void Compile_InvalidHlslSlotName_IsRejected()
        => Assert.Throws<InvalidDataException>(() => Compile(
            new[] { Custom("custom", "F", "Bad_Name = 1;", "out") }, "custom",
            Slot("out", 0, 1, 0, "Bad Name")));

    [Fact]
    public void Compile_UnconfiguredPlaceholder_IsRejected()
        => Assert.Throws<InvalidDataException>(() => Compile(
            new[] { Custom("custom", "Enter function name here...", "Enter function body here...", "out") },
            "custom", Slot("out", 0, 1, 0)));

    [Fact]
    public void Compile_FunctionWithoutOutput_IsRejected()
        => Assert.Throws<NotSupportedException>(() => Compile(
            new[] { Custom("custom", "F", "float value = 1;", "in") }, "custom", Slot("in", 0, 0, 0)));

    [Fact]
    public void Compile_FunctionWithMultipleOutputs_IsRejected()
        => Assert.Throws<NotSupportedException>(() => Compile(
            new[] { Custom("custom", "F", "A = 1; B = 2;", "a", "b") }, "custom",
            Slot("a", 0, 1, 0, "A"), Slot("b", 1, 1, 0, "B")));

    [Fact]
    public void Compile_VectorInput_IsExplicitlyRejectedByScalarCompiler()
        => Assert.Throws<NotSupportedException>(() => Compile(
            new[] { Custom("custom", "F", "Out = In.x;", "in", "out") }, "custom",
            Slot("in", 0, 0, 0, "In", "Vector2MaterialSlot"), Slot("out", 1, 1, 0)));

    [Fact]
    public void Compile_EdgeFromWrongCustomOutputSlot_IsRejected()
    {
        string[] nodes =
        {
            Custom("custom", "F", "Out = 1;", "custom-out"),
            Binary("sum", "sum-a", "sum-b", "sum-out")
        };
        string[] slots =
        {
            Slot("custom-out", 5, 1, 0), Slot("sum-a", 0, 0, 0),
            Slot("sum-b", 1, 0, 1), Slot("sum-out", 2, 1, 0)
        };

        Assert.Throws<InvalidDataException>(() => Compile(nodes, "sum", slots, Edge("custom", 99, "sum", 0)));
    }

    private static string CompilePair(string firstBody, string secondBody)
    {
        string[] nodes =
        {
            Custom("left", "Shared", firstBody, "left-in", "left-out"),
            Custom("right", "Shared", secondBody, "right-in", "right-out"),
            Binary("sum", "sum-a", "sum-b", "sum-out")
        };
        string[] slots =
        {
            Slot("left-in", 0, 0, 1, "In"), Slot("left-out", 1, 1, 0, "Out"),
            Slot("right-in", 0, 0, 2, "In"), Slot("right-out", 1, 1, 0, "Out"),
            Slot("sum-a", 0, 0, 0), Slot("sum-b", 1, 0, 0), Slot("sum-out", 2, 1, 0)
        };
        return Compile(nodes, "sum", slots, new[]
        {
            Edge("left", 1, "sum", 0), Edge("right", 1, "sum", 1)
        });
    }

    private static string Compile(string[] nodes, string output, params string[] slots)
        => Compile(nodes, output, slots, Array.Empty<string>());

    private static string Compile(
        string[] nodes,
        string output,
        string[] slots,
        string edge,
        Func<string, string>? includePathResolver = null)
        => Compile(nodes, output, slots, new[] { edge }, includePathResolver);

    private static string Compile(
        string[] nodes,
        string output,
        string[] slots,
        string[]? edges = null,
        Func<string, string>? includePathResolver = null)
    {
        string graph = "{\"m_SGVersion\":3,\"m_Type\":\"UnityEditor.ShaderGraph.GraphData\",\"m_ObjectId\":\"graph\",\"m_Nodes\":[" +
                       string.Join(",", nodes.Select(node => "{\"m_Id\":\"" + ReadId(node) + "\"}")) +
                       "],\"m_Edges\":[" + string.Join(",", edges ?? Array.Empty<string>()) + "]}";
        string source = graph + "\n" + string.Join("\n", nodes.Concat(slots));
        return ScalarHlslCompiler.Compile(MultiJsonAsset.Parse(source), output, includePathResolver);
    }

    private static string Custom(string id, string name, string body, params string[] slots)
        => Function(id, 1, name, string.Empty, body, slots);

    private static string CustomFile(string id, string name, string guid, params string[] slots)
        => Function(id, 0, name, guid, "Enter function body here...", slots);

    private static string Function(string id, int sourceType, string name, string source, string body, string[] slots)
        => "{\"m_SGVersion\":0,\"m_Type\":\"UnityEditor.ShaderGraph.CustomFunctionNode\",\"m_ObjectId\":\"" + id +
           "\",\"m_SourceType\":" + sourceType + ",\"m_FunctionName\":\"" + Escape(name) +
           "\",\"m_FunctionSource\":\"" + Escape(source) + "\",\"m_FunctionBody\":\"" + Escape(body) +
           "\",\"m_Slots\":[" + string.Join(",", slots.Select(slot => "{\"m_Id\":\"" + slot + "\"}")) + "]}";

    private static string Constant(string id, double value)
        => "{\"m_SGVersion\":0,\"m_Type\":\"UnityEditor.ShaderGraph.Vector1Node\",\"m_ObjectId\":\"" + id +
           "\",\"m_Value\":" + Number(value) + ",\"m_Slots\":[{\"m_Id\":\"" + id + "-in\"},{\"m_Id\":\"" + id + "-out\"}]}\n" +
           Slot(id + "-in", 0, 0, value) + "\n" + Slot(id + "-out", 1, 1, value);

    private static string Binary(string id, string inputA, string inputB, string output)
        => "{\"m_SGVersion\":0,\"m_Type\":\"UnityEditor.ShaderGraph.AddNode\",\"m_ObjectId\":\"" + id +
           "\",\"m_Slots\":[{\"m_Id\":\"" + inputA + "\"},{\"m_Id\":\"" + inputB + "\"},{\"m_Id\":\"" + output + "\"}]}";

    private static string Slot(
        string id,
        int slotId,
        int direction,
        double value,
        string? shaderName = null,
        string type = "Vector1MaterialSlot")
    {
        string name = shaderName ?? (direction == 0 ? "In" : "Out");
        return "{\"m_SGVersion\":0,\"m_Type\":\"UnityEditor.ShaderGraph." + type + "\",\"m_ObjectId\":\"" + id +
               "\",\"m_Id\":" + slotId + ",\"m_SlotType\":" + direction + ",\"m_DisplayName\":\"" + Escape(name) +
               "\",\"m_ShaderOutputName\":\"" + Escape(name) + "\",\"m_Value\":" + Number(value) + "}";
    }

    private static string Edge(string outputNode, int outputSlot, string inputNode, int inputSlot)
        => "{\"m_OutputSlot\":{\"m_Node\":{\"m_Id\":\"" + outputNode + "\"},\"m_SlotId\":" + outputSlot +
           "},\"m_InputSlot\":{\"m_Node\":{\"m_Id\":\"" + inputNode + "\"},\"m_SlotId\":" + inputSlot + "}}";

    private static string ReadId(string json)
    {
        const string marker = "\"m_ObjectId\":\"";
        int start = json.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        int end = json.IndexOf('\"', start);
        return json.Substring(start, end - start);
    }

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");

    private static int Count(string value, string pattern)
    {
        int count = 0;
        for (int index = 0; (index = value.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0; index += pattern.Length) count++;
        return count;
    }
}
