using UnityEditor.VFX.Generation;
using UnityEditor.VFX.Model;
using UnityEditor.VFX.Serialization;
using Xunit;

namespace Unity.VisualEffectGraph.Editor.Tests;

public sealed class VfxExpressionCompilerTests
{
    [Fact]
    public void Float2Add_EmitsTypedComponentWiseHlsl()
    {
        VfxExpressionCompilation result = Compile(BinaryGraph(AddGuid, SlotKind.Float2, SpaceNone,
            new[] { 1.0, 2.0 }, new[] { 3.0, 4.0 }), 103);

        Assert.Contains("float2 vfx_slot_101 = float2(1.0, 2.0);", result.HlslSource, StringComparison.Ordinal);
        Assert.Contains("float2 vfx_slot_103 = (vfx_slot_101 + vfx_slot_102);", result.HlslSource, StringComparison.Ordinal);
        Assert.Equal("Float2", result.ResultType.ToString());
    }

    [Fact]
    public void Float3Subtract_EmitsTypedHlsl()
    {
        VfxExpressionCompilation result = Compile(BinaryGraph(SubtractGuid, SlotKind.Float3, SpaceNone,
            new[] { 5.0, 4.0, 3.0 }, new[] { 1.0, 2.0, 3.0 }), 103);

        Assert.Contains("float3 vfx_slot_103 = (vfx_slot_101 - vfx_slot_102);", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LinkedInputRoot_CompilesDestinationVariableAndReportsHlslType()
    {
        VfxTypedGraph graph = VfxTypedGraph.Build(VfxYamlAsset.Parse(LinkedGraph(
            SlotKind.Position, SpaceLocal, SpaceWorld)));

        VfxExpressionCompilation result = VfxExpressionCompiler.CompileInput(graph, 101);

        Assert.Equal("float3", result.HlslType);
        Assert.Equal("vfx_slot_101", result.ResultVariable);
        Assert.Contains("mul(localToWorld, float4(vfx_slot_90, 1.0)).xyz", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VectorMultiply_UsesFloat3StorageAndPreservesSpace()
    {
        VfxExpressionCompilation result = Compile(BinaryGraph(MultiplyGuid, SlotKind.Vector, SpaceWorld,
            new[] { 1.0, 2.0, 3.0 }, new[] { 2.0, 2.0, 2.0 }), 103);

        Assert.Contains("float3 vfx_slot_103 = (vfx_slot_101 * vfx_slot_102);", result.HlslSource, StringComparison.Ordinal);
        Assert.Equal(VfxCoordinateSpace.World, result.ResultSpace);
    }

    [Fact]
    public void PositionLink_LocalToWorld_UsesOfficialTransformPositionFormula()
    {
        VfxExpressionCompilation result = Compile(LinkedGraph(SlotKind.Position, SpaceLocal, SpaceWorld), 103);

        Assert.Contains(
            "float3 vfx_slot_101 = mul(localToWorld, float4(vfx_slot_90, 1.0)).xyz;",
            result.HlslSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DirectionLink_WorldToLocal_UsesOfficialNormalizedDirectionFormula()
    {
        VfxExpressionCompilation result = Compile(LinkedGraph(SlotKind.Direction, SpaceWorld, SpaceLocal), 103);

        Assert.Contains(
            "float3 vfx_slot_101 = normalize(mul((float3x3)worldToLocal, vfx_slot_90));",
            result.HlslSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VectorLink_LocalToWorld_UsesOfficialVectorFormulaWithoutNormalization()
    {
        VfxExpressionCompilation result = Compile(LinkedGraph(SlotKind.Vector, SpaceLocal, SpaceWorld), 103);

        Assert.Contains("float3 vfx_slot_101 = mul((float3x3)localToWorld, vfx_slot_90);", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("normalize", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SameCoordinateSpace_DoesNotInsertTransform()
    {
        VfxExpressionCompilation result = Compile(LinkedGraph(SlotKind.Position, SpaceLocal, SpaceLocal), 103);

        Assert.Contains("float3 vfx_slot_101 = vfx_slot_90;", result.HlslSource, StringComparison.Ordinal);
        Assert.DoesNotContain("mul(", result.HlslSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SpaceNone, SpaceWorld)]
    [InlineData(SpaceLocal, SpaceNone)]
    public void NoneCoordinateSpace_BypassesConversionLikeUnity14(int sourceSpace, int destinationSpace)
    {
        VfxExpressionCompilation result = Compile(LinkedGraph(SlotKind.Position, sourceSpace, destinationSpace), 103);

        Assert.Contains("float3 vfx_slot_101 = vfx_slot_90;", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LinkedSemanticTypeMismatch_IsRejected()
    {
        string source = LinkedGraph(SlotKind.Vector, SlotKind.Position, SpaceLocal, SpaceWorld);

        Assert.Throws<InvalidDataException>(() => Compile(source, 103));
    }

    [Fact]
    public void OperatorInputOutputSpaceMismatch_IsRejected()
    {
        string source = BinaryGraph(AddGuid, SlotKind.Position, SpaceWorld,
            new[] { 1.0, 2.0, 3.0 }, new[] { 4.0, 5.0, 6.0 });
        source = ReplaceSlotSpace(source, 101, SpaceLocal);

        Assert.Throws<InvalidDataException>(() => Compile(source, 103));
    }

    [Fact]
    public void Float2OneMinus_EmitsVectorOneLiteral()
    {
        VfxExpressionCompilation result = Compile(UnaryGraph(OneMinusGuid, SlotKind.Float2, SpaceNone, new[] { 0.25, 0.5 }), 102);

        Assert.Contains("(float2(1.0, 1.0) - vfx_slot_101)", result.HlslSource, StringComparison.Ordinal);
    }

    [Fact]
    public void OneMinusSpaceableValue_IsRejected()
        => Assert.Throws<NotSupportedException>(() => Compile(
            UnaryGraph(OneMinusGuid, SlotKind.Position, SpaceLocal, new[] { 1.0, 2.0, 3.0 }), 102));

    [Fact]
    public void NonSpaceableSlotWithWorldSpace_IsRejected()
        => Assert.Throws<InvalidDataException>(() => Compile(
            BinaryGraph(AddGuid, SlotKind.Float3, SpaceWorld, new[] { 1.0, 2.0, 3.0 }, new[] { 4.0, 5.0, 6.0 }), 103));

    [Fact]
    public void NonFiniteVectorComponent_IsRejected()
        => Assert.Throws<InvalidDataException>(() => Compile(
            BinaryGraph(AddGuid, SlotKind.Float2, SpaceNone, new[] { double.NaN, 1.0 }, new[] { 2.0, 3.0 }), 103));

    [Fact]
    public void UnsupportedColorSlot_IsRejectedInsteadOfGeneratingPlaceholder()
    {
        string source = BinaryGraph(AddGuid, SlotKind.Color, SpaceNone,
            new[] { 1.0, 0.5, 0.25, 1.0 }, new[] { 0.0, 0.5, 0.75, 1.0 });

        Assert.Throws<NotSupportedException>(() => Compile(source, 103));
    }

    private static VfxExpressionCompilation Compile(string source, long outputSlotId)
        => VfxExpressionCompiler.Compile(VfxTypedGraph.Build(VfxYamlAsset.Parse(source)), outputSlotId);

    private static string BinaryGraph(string operatorGuid, SlotKind kind, int space, double[] first, double[] second)
        => Preamble + GraphDocument(new long[] { 20 }) +
           OperatorDocument(20, operatorGuid, new long[] { 101, 102 }, new long[] { 103 }) +
           SlotDocument(101, 20, 0, kind, space, first, Array.Empty<long>()) +
           SlotDocument(102, 20, 0, kind, space, second, Array.Empty<long>()) +
           SlotDocument(103, 20, 1, kind, space, Zero(kind), Array.Empty<long>()) +
           ResourceDocument();

    private static string UnaryGraph(string operatorGuid, SlotKind kind, int space, double[] input)
        => Preamble + GraphDocument(new long[] { 20 }) +
           OperatorDocument(20, operatorGuid, new long[] { 101 }, new long[] { 102 }) +
           SlotDocument(101, 20, 0, kind, space, input, Array.Empty<long>()) +
           SlotDocument(102, 20, 1, kind, space, Zero(kind), Array.Empty<long>()) +
           ResourceDocument();

    private static string LinkedGraph(SlotKind kind, int sourceSpace, int destinationSpace)
        => LinkedGraph(kind, kind, sourceSpace, destinationSpace);

    private static string LinkedGraph(
        SlotKind sourceKind,
        SlotKind destinationKind,
        int sourceSpace,
        int destinationSpace)
        => Preamble + GraphDocument(new long[] { 20 }) +
           OperatorDocument(20, AddGuid, new long[] { 101, 102 }, new long[] { 103 }) +
           SlotDocument(90, 0, 1, sourceKind, sourceSpace, Ones(sourceKind), new long[] { 101 }) +
           SlotDocument(101, 20, 0, destinationKind, destinationSpace, Zero(destinationKind), new long[] { 90 }) +
           SlotDocument(102, 20, 0, destinationKind, destinationSpace, Ones(destinationKind), Array.Empty<long>()) +
           SlotDocument(103, 20, 1, destinationKind, destinationSpace, Zero(destinationKind), Array.Empty<long>()) +
           ResourceDocument();

    private static string GraphDocument(IReadOnlyList<long> children)
        => "--- !u!114 &10\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {GraphGuid}, type: 3}}\n" +
           "  m_Name: Graph\n  m_Parent: {fileID: 0}\n" + ReferenceList("m_Children", children) +
           "  m_InputSlots: []\n  m_OutputSlots: []\n";

    private static string OperatorDocument(long fileId, string guid, IReadOnlyList<long> inputs, IReadOnlyList<long> outputs)
        => $"--- !u!114 &{fileId}\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n" +
           $"  m_Name: Operator{fileId}\n  m_Parent: {{fileID: 10}}\n  m_Children: []\n" +
           ReferenceList("m_InputSlots", inputs) + ReferenceList("m_OutputSlots", outputs);

    private static string SlotDocument(
        long fileId,
        long ownerId,
        int direction,
        SlotKind kind,
        int space,
        double[] components,
        IReadOnlyList<long> links)
    {
        string serializedType = SerializedType(kind);
        return $"--- !u!114 &{fileId}\nMonoBehaviour:\n" +
               $"  m_Script: {{fileID: 11500000, guid: {Guid(kind)}, type: 3}}\n" +
               $"  m_Name: Slot{fileId}\n  m_Parent: {{fileID: 0}}\n  m_Children: []\n" +
               $"  m_MasterSlot: {{fileID: {fileId}}}\n  m_MasterData:\n    m_Owner: {{fileID: {ownerId}}}\n" +
               $"    m_Value:\n      m_Type:\n        m_SerializableType: {serializedType}, assembly\n" +
               $"      m_SerializableObject: {Value(kind, components)}\n    m_Space: {space}\n" +
               $"  m_Property:\n    name: Value\n    m_serializedType:\n      m_SerializableType: {serializedType}, assembly\n" +
               $"  m_Direction: {direction}\n" + ReferenceList("m_LinkedSlots", links);
    }

    private static string Value(SlotKind kind, IReadOnlyList<double> values)
    {
        string[] names = kind switch
        {
            SlotKind.Float2 => new[] { "x", "y" },
            SlotKind.Color => new[] { "r", "g", "b", "a" },
            _ => new[] { "x", "y", "z" }
        };
        string content = string.Join(",", names.Select((name, index) => "\"" + name + "\":" + Literal(values[index])));
        string? wrapper = kind switch
        {
            SlotKind.Position => "position",
            SlotKind.Direction => "direction",
            SlotKind.Vector => "vector",
            _ => null
        };
        return wrapper is null ? "'{" + content + "}'" : "'{\"" + wrapper + "\":{" + content + "}}'";
    }

    private static double[] Zero(SlotKind kind) => new double[ComponentCount(kind)];

    private static double[] Ones(SlotKind kind) => Enumerable.Repeat(1.0, ComponentCount(kind)).ToArray();

    private static int ComponentCount(SlotKind kind) => kind switch
    {
        SlotKind.Float2 => 2,
        SlotKind.Color => 4,
        _ => 3
    };

    private static string SerializedType(SlotKind kind) => kind switch
    {
        SlotKind.Float2 => "UnityEngine.Vector2",
        SlotKind.Float3 => "UnityEngine.Vector3",
        SlotKind.Color => "UnityEngine.Color",
        SlotKind.Position => "UnityEditor.VFX.Position",
        SlotKind.Direction => "UnityEditor.VFX.DirectionType",
        SlotKind.Vector => "UnityEditor.VFX.Vector",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static string Guid(SlotKind kind) => kind switch
    {
        SlotKind.Float2 => Float2SlotGuid,
        SlotKind.Float3 => Float3SlotGuid,
        SlotKind.Color => ColorSlotGuid,
        SlotKind.Position => PositionSlotGuid,
        SlotKind.Direction => DirectionSlotGuid,
        SlotKind.Vector => VectorSlotGuid,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static string ReplaceSlotSpace(string source, long fileId, int replacement)
    {
        int document = source.IndexOf($"--- !u!114 &{fileId}\n", StringComparison.Ordinal);
        int next = source.IndexOf("--- !u!", document + 1, StringComparison.Ordinal);
        int space = source.IndexOf("    m_Space: ", document, StringComparison.Ordinal);
        if (space < 0 || (next >= 0 && space >= next)) throw new InvalidOperationException("Slot space was not found.");
        int end = source.IndexOf('\n', space);
        return source.Substring(0, space) + "    m_Space: " + replacement + source.Substring(end);
    }

    private static string ResourceDocument()
        => "--- !u!2058629511 &900\nVisualEffectResource:\n  m_Graph: {fileID: 10}\n";

    private static string ReferenceList(string fieldName, IReadOnlyList<long> values)
        => values.Count == 0
            ? $"  {fieldName}: []\n"
            : $"  {fieldName}:\n" + string.Concat(values.Select(value => $"  - {{fileID: {value}}}\n"));

    private static string Literal(double value)
        => double.IsNaN(value) ? "NaN" : value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    private enum SlotKind
    {
        Float2,
        Float3,
        Color,
        Position,
        Direction,
        Vector
    }

    private const int SpaceLocal = 0;
    private const int SpaceWorld = 1;
    private const int SpaceNone = int.MaxValue;
    private const string Preamble = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";
    private const string GraphGuid = "7d4c867f6b72b714dbb5fd1780afe208";
    private const string AddGuid = "c7acf5424f3655744af4b8f63298fa0f";
    private const string SubtractGuid = "0155ae97d9a75e3449c6d0603b79c2f4";
    private const string MultiplyGuid = "b8ee8a7543fa09e42a7c8616f60d2ad7";
    private const string OneMinusGuid = "c8ac0ebcb5fd27b408f3700034222acb";
    private const string Float2SlotGuid = "1b2b751071c7fc14f9fa503163991826";
    private const string Float3SlotGuid = "ac39bd03fca81b849929b9c966f1836a";
    private const string PositionSlotGuid = "5265657162cc1a241bba03a3b0476d99";
    private const string DirectionSlotGuid = "e8f2b4a846fd4c14a893cde576ad172b";
    private const string VectorSlotGuid = "a9f9544b71b7dab44a4644b6807e8bf6";
    private const string ColorSlotGuid = "c82227d5759e296488798b1554a72a15";
}
