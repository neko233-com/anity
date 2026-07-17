using UnityEditor.VFX.Generation;
using UnityEditor.VFX.Model;
using UnityEditor.VFX.Serialization;
using UnityEngine.VFX;
using Xunit;

namespace Unity.VisualEffectGraph.Editor.Tests;

public sealed class VfxRuntimeExpressionCompilerTests
{
    [Theory]
    [InlineData(AddGuid, 3)]
    [InlineData(SubtractGuid, 4)]
    [InlineData(MultiplyGuid, 5)]
    public void BinaryFloatOperator_CompilesOrderedTypedInstructions(
        string operatorGuid,
        int operationValue)
    {
        var operation = (VFXRuntimeExpressionOperation)operationValue;
        VFXRuntimeExpressionProgramData program = CompileInput(
            BinaryGraph(operatorGuid, "2", "3"), 104);

        Assert.Equal(VFXRuntimeValueType.Float, program.ResultType);
        Assert.Equal(2, program.ResultIndex);
        Assert.Equal(new[]
        {
            VFXRuntimeExpressionOperation.Constant,
            VFXRuntimeExpressionOperation.Constant,
            operation
        }, program.Instructions.Select(instruction => instruction.Operation));
        Assert.Equal(0, program.Instructions[2].InputA);
        Assert.Equal(1, program.Instructions[2].InputB);
    }

    [Fact]
    public void OneMinusFloat_CompilesUnaryInstruction()
    {
        VFXRuntimeExpressionProgramData program = CompileInput(UnaryGraph(OneMinusGuid, "0.25"), 103);

        Assert.Equal(1, program.ResultIndex);
        Assert.Equal(VFXRuntimeExpressionOperation.OneMinus, program.Instructions[1].Operation);
        Assert.Equal(0, program.Instructions[1].InputA);
        Assert.Equal(-1, program.Instructions[1].InputB);
    }

    [Fact]
    public void ExposedParameter_CompilesPropertyInstruction()
    {
        VFXRuntimeExpressionProgramData program = CompileInput(ExposedGraph(), 104);

        VFXRuntimeExpressionInstructionData property = Assert.Single(program.Instructions);
        Assert.Equal(VFXRuntimeExpressionOperation.ExposedProperty, property.Operation);
        Assert.Equal("Dynamic Delta", property.PropertyName);
        Assert.Equal(VFXRuntimeValueType.Float, property.ValueType);
    }

    [Fact]
    public void NestedOperators_CompileDependencyOrderedSsa()
    {
        VFXRuntimeExpressionProgramData program = CompileInput(ChainedGraph(), 204);

        Assert.Equal(new[]
        {
            VFXRuntimeExpressionOperation.Constant,
            VFXRuntimeExpressionOperation.Constant,
            VFXRuntimeExpressionOperation.Add,
            VFXRuntimeExpressionOperation.Constant,
            VFXRuntimeExpressionOperation.Multiply
        }, program.Instructions.Select(instruction => instruction.Operation));
        Assert.Equal(2, program.Instructions[4].InputA);
        Assert.Equal(3, program.Instructions[4].InputB);
        Assert.Equal(4, program.ResultIndex);
    }

    [Fact]
    public void SharedSource_IsEmittedOnlyOnce()
    {
        VFXRuntimeExpressionProgramData program = CompileInput(SharedGraph(), 204);

        Assert.Equal(4, program.Instructions.Count);
        Assert.Equal(VFXRuntimeExpressionOperation.Add, program.Instructions[2].Operation);
        Assert.Equal(2, program.Instructions[3].InputA);
        Assert.Equal(2, program.Instructions[3].InputB);
    }

    [Fact]
    public void Compile_IsDeterministic()
    {
        string source = ChainedGraph();

        VFXRuntimeExpressionProgramData first = CompileInput(source, 204);
        VFXRuntimeExpressionProgramData second = CompileInput(source, 204);
        Assert.Equal(Signature(first), Signature(second));
    }

    [Fact]
    public void OutputSlotRoot_IsRejected()
        => Assert.Throws<InvalidDataException>(() =>
            VfxRuntimeExpressionCompiler.CompileInput(Graph(BinaryGraph(AddGuid, "1", "2")), 103));

    [Fact]
    public void UnsupportedOperator_IsRejected()
        => Assert.Throws<NotSupportedException>(() =>
            CompileInput(BinaryGraph(RandomGuid, "1", "2"), 104));

    [Fact]
    public void WrongBinaryArity_IsRejected()
    {
        string source = Preamble +
                        GraphDocument(new long[] { 20 }) +
                        OperatorDocument(20, AddGuid, new long[] { 101 }, new long[] { 103 }) +
                        FloatSlot(101, 20, 0, "1", Array.Empty<long>()) +
                        FloatSlot(103, 20, 1, "0", new long[] { 104 }) +
                        FloatSlot(104, 0, 0, "0", new long[] { 103 }) +
                        ResourceDocument();

        Assert.Throws<InvalidDataException>(() => CompileInput(source, 104));
    }

    [Fact]
    public void OneMinusInteger_IsRejected()
    {
        string source = Preamble +
                        GraphDocument(new long[] { 20 }) +
                        OperatorDocument(20, OneMinusGuid, new long[] { 101 }, new long[] { 102 }) +
                        IntSlot(101, 20, 0, "1", Array.Empty<long>()) +
                        IntSlot(102, 20, 1, "0", new long[] { 103 }) +
                        IntSlot(103, 0, 0, "0", new long[] { 102 }) +
                        ResourceDocument();

        Assert.Throws<NotSupportedException>(() => CompileInput(source, 103));
    }

    [Theory]
    [InlineData(1, 7, "float")]
    [InlineData(2, 8, "float")]
    [InlineData(4, 9, "float")]
    [InlineData(8, 10, "uint")]
    [InlineData(16, 11, "float")]
    [InlineData(32, 12, "float")]
    [InlineData(64, 13, "float")]
    [InlineData(128, 14, "float")]
    [InlineData(256, 15, "float")]
    [InlineData(512, 16, "float")]
    [InlineData(1024, 17, "float")]
    [InlineData(2048, 18, "float")]
    [InlineData(4096, 19, "float")]
    [InlineData(8192, 20, "float")]
    [InlineData(16384, 21, "matrix")]
    [InlineData(32768, 22, "matrix")]
    [InlineData(65536, 23, "uint")]
    public void DynamicBuiltInParameter_CompilesAllOfficialVfxGraph14Flags(
        int flag,
        int operation,
        string type)
    {
        VFXRuntimeExpressionProgramData program = CompileInput(BuiltInGraph(flag, type), 401);

        VFXRuntimeExpressionInstructionData instruction = Assert.Single(program.Instructions);
        Assert.Equal((VFXRuntimeExpressionOperation)operation, instruction.Operation);
        Assert.Equal(type switch
        {
            "uint" => VFXRuntimeValueType.UInt32,
            "matrix" => VFXRuntimeValueType.Matrix4x4,
            _ => VFXRuntimeValueType.Float
        }, instruction.ValueType);
        Assert.Equal(-1, instruction.InputA);
        Assert.Equal(-1, instruction.InputB);
    }

    [Fact]
    public void DynamicBuiltInParameter_MapsCombinedOutputsInAscendingFlagOrder()
    {
        string source = BuiltInGraph(1 | 8 | 65536, "float", "uint", "uint");

        Assert.Equal(VFXRuntimeExpressionOperation.VfxDeltaTime,
            Assert.Single(CompileInput(source, 401).Instructions).Operation);
        Assert.Equal(VFXRuntimeExpressionOperation.VfxFrameIndex,
            Assert.Single(CompileInput(source, 402).Instructions).Operation);
        Assert.Equal(VFXRuntimeExpressionOperation.SystemSeed,
            Assert.Single(CompileInput(source, 403).Instructions).Operation);
    }

    [Fact]
    public void DynamicBuiltInParameter_RejectsUnknownFlags()
        => Assert.Throws<InvalidDataException>(() => CompileInput(BuiltInGraph(1 << 20, "float"), 401));

    [Fact]
    public void DynamicBuiltInParameter_RejectsOutputCountMismatch()
        => Assert.Throws<InvalidDataException>(() => CompileInput(BuiltInGraph(1 | 2, "float"), 401));

    [Fact]
    public void DynamicBuiltInParameter_RejectsOfficialTypeMismatch()
        => Assert.Throws<InvalidDataException>(() => CompileInput(BuiltInGraph(8, "float"), 401));

    private static VFXRuntimeExpressionProgramData CompileInput(string source, long inputSlotId)
        => VfxRuntimeExpressionCompiler.CompileInput(Graph(source), inputSlotId);

    private static VfxTypedGraph Graph(string source)
        => VfxTypedGraph.Build(VfxYamlAsset.Parse(source));

    private static string Signature(VFXRuntimeExpressionProgramData program)
        => $"{program.ResultType}:{program.ResultIndex}:" + string.Join(";", program.Instructions.Select(
            instruction => $"{instruction.Operation},{instruction.ValueType},{instruction.InputA}," +
                           $"{instruction.InputB},{instruction.PropertyName}," +
                           string.Join(",", instruction.ConstantWords)));

    private static string BinaryGraph(string operatorGuid, string first, string second)
        => Preamble +
           GraphDocument(new long[] { 20 }) +
           OperatorDocument(20, operatorGuid, new long[] { 101, 102 }, new long[] { 103 }) +
           FloatSlot(101, 20, 0, first, Array.Empty<long>()) +
           FloatSlot(102, 20, 0, second, Array.Empty<long>()) +
           FloatSlot(103, 20, 1, "0", new long[] { 104 }) +
           FloatSlot(104, 0, 0, "0", new long[] { 103 }) +
           ResourceDocument();

    private static string UnaryGraph(string operatorGuid, string value)
        => Preamble +
           GraphDocument(new long[] { 20 }) +
           OperatorDocument(20, operatorGuid, new long[] { 101 }, new long[] { 102 }) +
           FloatSlot(101, 20, 0, value, Array.Empty<long>()) +
           FloatSlot(102, 20, 1, "0", new long[] { 103 }) +
           FloatSlot(103, 0, 0, "0", new long[] { 102 }) +
           ResourceDocument();

    private static string ChainedGraph()
        => Preamble +
           GraphDocument(new long[] { 20, 30 }) +
           OperatorDocument(20, AddGuid, new long[] { 101, 102 }, new long[] { 103 }) +
           FloatSlot(101, 20, 0, "1", Array.Empty<long>()) +
           FloatSlot(102, 20, 0, "2", Array.Empty<long>()) +
           FloatSlot(103, 20, 1, "0", new long[] { 201 }) +
           OperatorDocument(30, MultiplyGuid, new long[] { 201, 202 }, new long[] { 203 }) +
           FloatSlot(201, 30, 0, "0", new long[] { 103 }) +
           FloatSlot(202, 30, 0, "3", Array.Empty<long>()) +
           FloatSlot(203, 30, 1, "0", new long[] { 204 }) +
           FloatSlot(204, 0, 0, "0", new long[] { 203 }) +
           ResourceDocument();

    private static string SharedGraph()
        => Preamble +
           GraphDocument(new long[] { 20, 30 }) +
           OperatorDocument(20, AddGuid, new long[] { 101, 102 }, new long[] { 103 }) +
           FloatSlot(101, 20, 0, "1", Array.Empty<long>()) +
           FloatSlot(102, 20, 0, "2", Array.Empty<long>()) +
           FloatSlot(103, 20, 1, "0", new long[] { 201, 202 }) +
           OperatorDocument(30, MultiplyGuid, new long[] { 201, 202 }, new long[] { 203 }) +
           FloatSlot(201, 30, 0, "0", new long[] { 103 }) +
           FloatSlot(202, 30, 0, "0", new long[] { 103 }) +
           FloatSlot(203, 30, 1, "0", new long[] { 204 }) +
           FloatSlot(204, 0, 0, "0", new long[] { 203 }) +
           ResourceDocument();

    private static string ExposedGraph()
        => Preamble +
           GraphDocument(new long[] { 30 }) +
           ExposedParameter(30, 301, "Dynamic Delta") +
           FloatSlot(301, 30, 1, "6", new long[] { 104 }) +
           FloatSlot(104, 0, 0, "0", new long[] { 301 }) +
           ResourceDocument();

    private static string BuiltInGraph(int flags, params string[] types)
    {
        long[] outputs = Enumerable.Range(0, types.Length).Select(index => 301L + index).ToArray();
        string slots = string.Concat(types.Select((type, index) =>
            TypedSlot(301 + index, 30, 1, type, new[] { 401L + index }) +
            TypedSlot(401 + index, 0, 0, type, new[] { 301L + index })));
        return Preamble +
               GraphDocument(new long[] { 30 }) +
               BuiltInParameter(30, outputs, flags) +
               slots +
               ResourceDocument();
    }

    private static string BuiltInParameter(
        long fileId,
        IReadOnlyList<long> outputSlotIds,
        int flags)
        => $"--- !u!114 &{fileId}\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {BuiltInParameterGuid}, type: 3}}\n" +
           "  m_Name: \n  m_Parent: {fileID: 10}\n  m_Children: []\n  m_InputSlots: []\n" +
           ReferenceList("m_OutputSlots", outputSlotIds) +
           $"  m_BuiltInParameters: {flags}\n";

    private static string TypedSlot(
        long fileId,
        long ownerId,
        int direction,
        string type,
        IReadOnlyList<long> links)
        => type switch
        {
            "uint" => SlotDocument(fileId, ownerId, direction, UIntSlotGuid,
                "System.UInt32", "0", links),
            "matrix" => SlotDocument(fileId, ownerId, direction, TransformSlotGuid,
                "UnityEditor.VFX.Transform",
                "'{\"position\":{\"x\":0,\"y\":0,\"z\":0},\"angles\":{\"x\":0,\"y\":0,\"z\":0},\"scale\":{\"x\":1,\"y\":1,\"z\":1}}'",
                links),
            _ => FloatSlot(fileId, ownerId, direction, "0", links)
        };

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

    private static string ExposedParameter(long fileId, long outputSlotId, string name)
        => $"--- !u!114 &{fileId}\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {ParameterGuid}, type: 3}}\n" +
           "  m_Name: \n  m_Parent: {fileID: 10}\n  m_Children: []\n  m_InputSlots: []\n" +
           $"  m_OutputSlots:\n  - {{fileID: {outputSlotId}}}\n" +
           $"  m_ExposedName: {name}\n  m_Exposed: 1\n  m_Order: 0\n  m_Category: \n" +
           "  m_Min:\n    m_Type:\n      m_SerializableType: \n    m_SerializableObject: \n" +
           "  m_Max:\n    m_Type:\n      m_SerializableType: \n    m_SerializableObject: \n" +
           "  m_IsOutput: 0\n  m_EnumValues: []\n  m_ValueFilter: 0\n  m_Tooltip: \n  m_Nodes: []\n";

    private static string FloatSlot(
        long fileId,
        long ownerId,
        int direction,
        string value,
        IReadOnlyList<long> links)
        => SlotDocument(fileId, ownerId, direction, FloatSlotGuid, "System.Single", value, links);

    private static string IntSlot(
        long fileId,
        long ownerId,
        int direction,
        string value,
        IReadOnlyList<long> links)
        => SlotDocument(fileId, ownerId, direction, IntSlotGuid, "System.Int32", value, links);

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
        => values.Count == 0
            ? $"  {fieldName}: []\n"
            : $"  {fieldName}:\n" + string.Concat(values.Select(value => $"  - {{fileID: {value}}}\n"));

    private const string Preamble = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";
    private const string GraphGuid = "7d4c867f6b72b714dbb5fd1780afe208";
    private const string ParameterGuid = "330e0fca1717dde4aaa144f48232aa64";
    private const string BuiltInParameterGuid = "a72fbb93ebe17974e90a144ef2ec8ceb";
    private const string AddGuid = "c7acf5424f3655744af4b8f63298fa0f";
    private const string SubtractGuid = "0155ae97d9a75e3449c6d0603b79c2f4";
    private const string MultiplyGuid = "b8ee8a7543fa09e42a7c8616f60d2ad7";
    private const string OneMinusGuid = "c8ac0ebcb5fd27b408f3700034222acb";
    private const string RandomGuid = "c42128e17c583714a909b4997c80c916";
    private const string FloatSlotGuid = "f780aa281814f9842a7c076d436932e7";
    private const string IntSlotGuid = "4d246e354feb93041a837a9ef59437cb";
    private const string UIntSlotGuid = "c52d920e7fff73b498050a6b3c4404ca";
    private const string TransformSlotGuid = "3e3f628d80ffceb489beac74258f9cf7";
}
