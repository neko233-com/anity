using UnityEditor.VFX.Generation;
using UnityEditor.VFX.Model;
using UnityEditor.VFX.Serialization;
using Xunit;

namespace Unity.VisualEffectGraph.Editor.Tests;

public sealed class VfxScalarExpressionCompilerTests
{
    [Theory]
    [InlineData(AddGuid, "+")]
    [InlineData(SubtractGuid, "-")]
    [InlineData(MultiplyGuid, "*")]
    public void BinaryOperator_EmitsDependencyOrderedHlsl(string operatorGuid, string operation)
    {
        VfxScalarCompilation compilation = Compile(BinaryGraph(operatorGuid, "1", "2"), 103);

        Assert.Equal(
            "float vfx_slot_101 = 1.0;\n" +
            "float vfx_slot_102 = 2.0;\n" +
            $"float vfx_slot_103 = (vfx_slot_101 {operation} vfx_slot_102);\n",
            compilation.HlslSource.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal(new long[] { 101, 102, 103 }, compilation.OrderedSlotIds);
    }

    [Fact]
    public void OneMinus_EmitsUnaryExpression()
    {
        VfxScalarCompilation compilation = Compile(UnaryGraph(OneMinusGuid, "0.25"), 102);

        Assert.Contains("float vfx_slot_102 = (1.0 - vfx_slot_101);", compilation.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LinkedOperators_CompileTransitiveDependencies()
    {
        VfxScalarCompilation compilation = Compile(ChainedGraph(), 203);

        Assert.Equal("vfx_slot_203", compilation.ResultVariable);
        Assert.Equal(new long[] { 101, 102, 103, 201, 202, 203 }, compilation.OrderedSlotIds);
        Assert.Contains("float vfx_slot_201 = vfx_slot_103;", compilation.HlslSource, StringComparison.Ordinal);
        Assert.Contains("float vfx_slot_203 = (vfx_slot_201 * vfx_slot_202);", compilation.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedOutputDependency_IsEmittedOnce()
    {
        string source = TwoOperatorGraph(
            new Dictionary<long, IReadOnlyList<long>>
            {
                [103] = new long[] { 201, 202 },
                [201] = new long[] { 103 },
                [202] = new long[] { 103 }
            },
            "3");
        VfxScalarCompilation compilation = Compile(source, 203);

        Assert.Equal(1, Count(compilation.HlslSource, "float vfx_slot_103 ="));
    }

    [Fact]
    public void Compile_IsDeterministic()
    {
        string source = ChainedGraph();

        Assert.Equal(Compile(source, 203).HlslSource, Compile(source, 203).HlslSource);
    }

    [Fact]
    public void RequestedInputSlot_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => Compile(BinaryGraph(AddGuid, "1", "2"), 101));
    }

    [Fact]
    public void MissingSlot_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => Compile(BinaryGraph(AddGuid, "1", "2"), 999));
    }

    [Fact]
    public void UnsupportedOperator_IsRejected()
    {
        Assert.Throws<NotSupportedException>(() => Compile(BinaryGraph(RandomGuid, "1", "2"), 103));
    }

    [Fact]
    public void WrongBinaryArity_IsRejected()
    {
        string source = Preamble +
                        GraphDocument(new long[] { 20 }) +
                        OperatorDocument(20, AddGuid, new long[] { 101 }, new long[] { 102 }) +
                        FloatSlot(101, 20, 0, "1", Array.Empty<long>()) +
                        FloatSlot(102, 20, 1, "0", Array.Empty<long>()) +
                        ResourceDocument();

        Assert.Throws<InvalidDataException>(() => Compile(source, 102));
    }

    [Fact]
    public void NonScalarSlot_IsRejected()
    {
        string source = BinaryGraph(AddGuid, "1", "2")
            .Replace(FloatSlot(103, 20, 1, "0", Array.Empty<long>()), Float3Slot(103, 20, 1), StringComparison.Ordinal);

        Assert.Throws<NotSupportedException>(() => Compile(source, 103));
    }

    [Fact]
    public void SlotDependencyCycle_IsRejected()
    {
        var links = new Dictionary<long, IReadOnlyList<long>>
        {
            [101] = new long[] { 103 },
            [103] = new long[] { 101 }
        };
        string source = Preamble +
                        GraphDocument(new long[] { 20 }) +
                        OperatorDocument(20, AddGuid, new long[] { 101, 102 }, new long[] { 103 }) +
                        FloatSlot(101, 20, 0, string.Empty, links[101]) +
                        FloatSlot(102, 20, 0, "1", Array.Empty<long>()) +
                        FloatSlot(103, 20, 1, "0", links[103]) +
                        ResourceDocument();

        Assert.Throws<InvalidDataException>(() => Compile(source, 103));
    }

    [Fact]
    public void NegativeFileId_UsesValidHlslIdentifier()
    {
        string source = Preamble +
                        GraphDocument(new long[] { 20 }) +
                        OperatorDocument(20, OneMinusGuid, new long[] { -1 }, new long[] { -2 }) +
                        FloatSlot(-1, 20, 0, "0", Array.Empty<long>()) +
                        FloatSlot(-2, 20, 1, "0", Array.Empty<long>()) +
                        ResourceDocument();

        Assert.Equal("vfx_slot_n2", Compile(source, -2).ResultVariable);
    }

    [Fact]
    public void NonFiniteConstant_IsRejectedDuringHlslGeneration()
    {
        Assert.Throws<InvalidDataException>(() => Compile(BinaryGraph(AddGuid, "NaN", "2"), 103));
    }

    private static VfxScalarCompilation Compile(string source, long slotId)
        => VfxScalarExpressionCompiler.Compile(VfxTypedGraph.Build(VfxYamlAsset.Parse(source)), slotId);

    private static string BinaryGraph(string operatorGuid, string first, string second)
        => Preamble +
           GraphDocument(new long[] { 20 }) +
           OperatorDocument(20, operatorGuid, new long[] { 101, 102 }, new long[] { 103 }) +
           FloatSlot(101, 20, 0, first, Array.Empty<long>()) +
           FloatSlot(102, 20, 0, second, Array.Empty<long>()) +
           FloatSlot(103, 20, 1, "0", Array.Empty<long>()) +
           ResourceDocument();

    private static string UnaryGraph(string operatorGuid, string input)
        => Preamble +
           GraphDocument(new long[] { 20 }) +
           OperatorDocument(20, operatorGuid, new long[] { 101 }, new long[] { 102 }) +
           FloatSlot(101, 20, 0, input, Array.Empty<long>()) +
           FloatSlot(102, 20, 1, "0", Array.Empty<long>()) +
           ResourceDocument();

    private static string ChainedGraph()
        => TwoOperatorGraph(
            new Dictionary<long, IReadOnlyList<long>>
            {
                [103] = new long[] { 201 },
                [201] = new long[] { 103 }
            },
            "4");

    private static string TwoOperatorGraph(
        IReadOnlyDictionary<long, IReadOnlyList<long>> links,
        string secondInput)
        => Preamble +
           GraphDocument(new long[] { 20, 30 }) +
           OperatorDocument(20, AddGuid, new long[] { 101, 102 }, new long[] { 103 }) +
           FloatSlot(101, 20, 0, "1", GetLinks(links, 101)) +
           FloatSlot(102, 20, 0, "2", GetLinks(links, 102)) +
           FloatSlot(103, 20, 1, "0", GetLinks(links, 103)) +
           OperatorDocument(30, MultiplyGuid, new long[] { 201, 202 }, new long[] { 203 }) +
           FloatSlot(201, 30, 0, string.Empty, GetLinks(links, 201)) +
           FloatSlot(202, 30, 0, secondInput, GetLinks(links, 202)) +
           FloatSlot(203, 30, 1, "0", GetLinks(links, 203)) +
           ResourceDocument();

    private static IReadOnlyList<long> GetLinks(
        IReadOnlyDictionary<long, IReadOnlyList<long>> links,
        long slotId)
        => links.TryGetValue(slotId, out IReadOnlyList<long>? result) ? result : Array.Empty<long>();

    private static string GraphDocument(IReadOnlyList<long> children)
        => "--- !u!114 &10\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {GraphGuid}, type: 3}}\n" +
           "  m_Name: Graph\n  m_Parent: {fileID: 0}\n" +
           ReferenceList("m_Children", children) +
           "  m_InputSlots: []\n  m_OutputSlots: []\n";

    private static string OperatorDocument(
        long fileId,
        string guid,
        IReadOnlyList<long> inputs,
        IReadOnlyList<long> outputs)
        => $"--- !u!114 &{fileId}\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n" +
           $"  m_Name: Operator{fileId}\n  m_Parent: {{fileID: 10}}\n  m_Children: []\n" +
           ReferenceList("m_InputSlots", inputs) +
           ReferenceList("m_OutputSlots", outputs);

    private static string FloatSlot(
        long fileId,
        long ownerId,
        int direction,
        string value,
        IReadOnlyList<long> links)
        => SlotDocument(
            fileId,
            ownerId,
            direction,
            FloatSlotGuid,
            "System.Single",
            value,
            links);

    private static string Float3Slot(long fileId, long ownerId, int direction)
        => SlotDocument(
            fileId,
            ownerId,
            direction,
            Float3SlotGuid,
            "UnityEngine.Vector3",
            "'{\"x\":0,\"y\":0,\"z\":0}'",
            Array.Empty<long>());

    private static string SlotDocument(
        long fileId,
        long ownerId,
        int direction,
        string scriptGuid,
        string serializedType,
        string value,
        IReadOnlyList<long> links)
        => $"--- !u!114 &{fileId}\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {scriptGuid}, type: 3}}\n" +
           $"  m_Name: Slot{fileId}\n  m_Parent: {{fileID: 0}}\n  m_Children: []\n" +
           $"  m_MasterSlot: {{fileID: {fileId}}}\n  m_MasterData:\n    m_Owner: {{fileID: {ownerId}}}\n" +
           $"    m_Value:\n      m_Type:\n        m_SerializableType: {serializedType}, assembly\n" +
           $"      m_SerializableObject: {value}\n    m_Space: 2147483647\n" +
           $"  m_Property:\n    name: Value\n    m_serializedType:\n      m_SerializableType: {serializedType}, assembly\n" +
           $"  m_Direction: {direction}\n" +
           ReferenceList("m_LinkedSlots", links);

    private static string ResourceDocument()
        => "--- !u!2058629511 &90\nVisualEffectResource:\n  m_Graph: {fileID: 10}\n";

    private static string ReferenceList(string fieldName, IReadOnlyList<long> values)
    {
        if (values.Count == 0) return $"  {fieldName}: []\n";
        return $"  {fieldName}:\n" + string.Concat(values.Select(value => $"  - {{fileID: {value}}}\n"));
    }

    private static int Count(string source, string value)
        => source.Split(new[] { value }, StringSplitOptions.None).Length - 1;

    private const string Preamble = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";
    private const string GraphGuid = "7d4c867f6b72b714dbb5fd1780afe208";
    private const string AddGuid = "c7acf5424f3655744af4b8f63298fa0f";
    private const string SubtractGuid = "0155ae97d9a75e3449c6d0603b79c2f4";
    private const string MultiplyGuid = "b8ee8a7543fa09e42a7c8616f60d2ad7";
    private const string OneMinusGuid = "c8ac0ebcb5fd27b408f3700034222acb";
    private const string RandomGuid = "c42128e17c583714a909b4997c80c916";
    private const string FloatSlotGuid = "f780aa281814f9842a7c076d436932e7";
    private const string Float3SlotGuid = "ac39bd03fca81b849929b9c966f1836a";
}
