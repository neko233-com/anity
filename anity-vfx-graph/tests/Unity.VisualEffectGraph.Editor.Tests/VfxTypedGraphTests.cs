using UnityEditor.VFX.Model;
using UnityEditor.VFX.Serialization;
using Xunit;

namespace Unity.VisualEffectGraph.Editor.Tests;

public sealed class VfxTypedGraphTests
{
    [Fact]
    public void Registry_ResolvesOfficialKindsAndNames()
    {
        Assert.Equal(72, VfxScriptTypeRegistry.All.Count);
        Assert.Equal("VFXGraph", VfxScriptTypeRegistry.Resolve(GraphGuid).TypeName);
        Assert.Equal(VfxModelKind.Context, VfxScriptTypeRegistry.Resolve(SpawnerGuid).Kind);
        Assert.Equal(VfxModelKind.Block, VfxScriptTypeRegistry.Resolve(RateGuid).Kind);
        Assert.Equal("VFXSpawnerSetAttribute", VfxScriptTypeRegistry.Resolve(SpawnerSetAttributeGuid).TypeName);
        Assert.Equal("VFXSpawnerCustomWrapper", VfxScriptTypeRegistry.Resolve(SpawnerCustomWrapperGuid).TypeName);
        Assert.Equal(VfxModelKind.Slot, VfxScriptTypeRegistry.Resolve(FloatSlotGuid).Kind);
    }

    [Fact]
    public void Registry_NormalizesUppercaseHyphenatedGuid()
    {
        Assert.True(VfxScriptTypeRegistry.TryResolve("7D4C867F-6B72-B714-DBB5-FD1780AFE208", out VfxScriptType? type));
        Assert.Equal("VFXGraph", type!.TypeName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("xyz")]
    [InlineData("7d4c867f6b72b714dbb5fd1780afe20z")]
    public void Registry_InvalidGuid_IsRejected(string guid)
    {
        Assert.Throws<ArgumentException>(() => VfxScriptTypeRegistry.TryResolve(guid, out _));
    }

    [Fact]
    public void Registry_UnknownValidGuid_ResolveThrows()
    {
        Assert.Throws<KeyNotFoundException>(() => VfxScriptTypeRegistry.Resolve(UnknownGuid));
    }

    [Fact]
    public void Build_IndexesTypedModels()
    {
        VfxTypedGraph graph = Build(BaseAsset());

        Assert.Equal(10, graph.Graph.FileId);
        Assert.Equal(2, graph.Contexts.Count);
        Assert.Single(graph.Blocks);
        Assert.Single(graph.Slots);
        Assert.Empty(graph.Operators);
    }

    [Fact]
    public void Build_PreservesHierarchy()
    {
        VfxTypedGraph graph = Build(BaseAsset());

        Assert.Equal(new long[] { 20, 30 }, graph.Graph.ChildrenIds);
        Assert.Equal(new long[] { 50 }, graph.ModelsByFileId[20].ChildrenIds);
        Assert.Equal(20, graph.ModelsByFileId[50].ParentId);
    }

    [Fact]
    public void Build_ModelsMasterSlotOwnerAndDirection()
    {
        VfxTypedGraph graph = Build(BaseAsset());
        VfxModel slot = graph.ModelsByFileId[60];

        Assert.Equal(60, slot.MasterSlotId);
        Assert.Equal(50, slot.OwnerId);
        Assert.Equal(0, slot.Direction);
        Assert.NotNull(slot.SlotProperty);
        Assert.Equal("Rate", slot.SlotProperty!.Name);
        Assert.Equal("System.Single", slot.SlotProperty.SerializedTypeName);
        Assert.Equal(VfxCoordinateSpace.None, slot.SlotProperty.Space);
        Assert.Equal(16d, slot.SlotProperty.Value.Scalar);
        Assert.Equal(new long[] { 60 }, graph.ModelsByFileId[50].InputSlotIds);
    }

    [Fact]
    public void Build_CreatesOneReciprocalFlowEdge()
    {
        VfxFlowEdge edge = Assert.Single(Build(BaseAsset()).FlowEdges);

        Assert.Equal(20, edge.SourceContextId);
        Assert.Equal(0, edge.SourceSlotIndex);
        Assert.Equal(30, edge.TargetContextId);
        Assert.Equal(0, edge.TargetSlotIndex);
    }

    [Fact]
    public void TopologicalSort_IsStableAndDependencyOrdered()
    {
        VfxTypedGraph graph = Build(BaseAsset());

        Assert.Equal(new long[] { 20, 30 }, graph.TopologicallySortContexts().Select(context => context.FileId));
    }

    [Fact]
    public void Build_ResourceMustReferenceGraphModel()
    {
        string source = BaseAsset().Replace("m_Graph: {fileID: 10}", "m_Graph: {fileID: 20}", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Build(source));
    }

    [Fact]
    public void Build_RejectsNonReciprocalHierarchy()
    {
        string source = BaseAsset().Replace("  m_Children:\n  - {fileID: 50}\n", "  m_Children: []\n", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Build(source));
    }

    [Fact]
    public void Build_RejectsUnresolvedSlotOwner()
    {
        string source = BaseAsset().Replace("    m_Owner: {fileID: 50}", "    m_Owner: {fileID: 999}", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Build(source));
    }

    [Fact]
    public void Build_RejectsSlotDirectionMismatch()
    {
        string source = BaseAsset().Replace("  m_Direction: 0", "  m_Direction: 1", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Build(source));
    }

    [Fact]
    public void Build_RejectsNonReciprocalFlow()
    {
        string source = BaseAsset().Replace(
            "  m_InputFlowSlot:\n  - link:\n    - context: {fileID: 20}\n      slotIndex: 0\n",
            "  m_InputFlowSlot:\n  - link: []\n",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Build(source));
    }

    [Fact]
    public void Build_RejectsFlowToNonContext()
    {
        string source = BaseAsset()
            .Replace("context: {fileID: 30}", "context: {fileID: 50}", StringComparison.Ordinal)
            .Replace("context: {fileID: 20}", "context: {fileID: 50}", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Build(source));
    }

    [Fact]
    public void Build_RejectsOutOfRangeFlowSlot()
    {
        string source = BaseAsset().Replace("      slotIndex: 0", "      slotIndex: 4", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Build(source));
    }

    [Fact]
    public void TopologicalSort_RejectsContextCycle()
    {
        string source = BaseAsset()
            .Replace(
                "  m_OutputFlowSlot:\n  - link: []\n" + BlockDocument(),
                "  m_OutputFlowSlot:\n  - link:\n    - context: {fileID: 20}\n      slotIndex: 0\n" + BlockDocument(),
                StringComparison.Ordinal)
            .Replace(
                "  m_InputFlowSlot:\n  - link: []\n  - link: []\n",
                "  m_InputFlowSlot:\n  - link:\n    - context: {fileID: 30}\n      slotIndex: 0\n  - link: []\n",
                StringComparison.Ordinal);
        VfxTypedGraph graph = Build(source);

        Assert.Throws<InvalidDataException>(() => graph.TopologicallySortContexts());
    }

    [Fact]
    public void Build_UnknownScriptIsPreservedAsUnsupportedModel()
    {
        VfxTypedGraph graph = Build(BaseAsset() + ModelDocument(70, UnknownGuid, 0, Array.Empty<long>()));
        VfxModel model = graph.ModelsByFileId[70];

        Assert.Equal(VfxModelKind.Unknown, model.Kind);
        Assert.False(model.ScriptType.IsProductSupported);
        Assert.Contains(model, graph.UnsupportedModels);
    }

    [Fact]
    public void Registry_HdrpSubOutputIsExplicitlyExternalAndUnsupported()
    {
        VfxScriptType type = VfxScriptTypeRegistry.Resolve("081ffb0090424ba4cb05370a42ead6b9");

        Assert.Equal(VfxModelKind.External, type.Kind);
        Assert.False(type.IsProductSupported);
    }

    [Fact]
    public void Build_MonoBehaviourWithoutScriptGuidIsRejected()
    {
        string source = BaseAsset() + "--- !u!114 &70\nMonoBehaviour:\n  m_Name: Missing Script\n";

        Assert.Throws<InvalidDataException>(() => Build(source));
    }

    [Fact]
    public void Build_RejectsInlineNonListSlotEncoding()
    {
        string source = BaseAsset().Replace("  m_InputSlots: []", "  m_InputSlots: invalid", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Build(source));
    }

    [Fact]
    public void Build_ValidatesReciprocalContextDataOwnership()
    {
        string source = BaseAsset()
            .Replace("  m_Name: Context20\n", "  m_Name: Context20\n  m_Data: {fileID: 80}\n", StringComparison.Ordinal) +
            DataDocument(80, new long[] { 20 });
        VfxTypedGraph graph = Build(source);

        Assert.Equal(80, graph.ModelsByFileId[20].DataId);
        Assert.Equal(new long[] { 20 }, graph.ModelsByFileId[80].OwnerIds);
        Assert.Single(graph.Data);
    }

    [Fact]
    public void Build_RejectsNonReciprocalDataOwner()
    {
        string source = BaseAsset()
            .Replace("  m_Name: Context20\n", "  m_Name: Context20\n  m_Data: {fileID: 80}\n", StringComparison.Ordinal) +
            DataDocument(80, Array.Empty<long>());

        Assert.Throws<InvalidDataException>(() => Build(source));
    }

    [Fact]
    public void Build_RejectsMalformedReferenceListEntry()
    {
        string source = BaseAsset().Replace("  - {fileID: 50}\n", "  - invalid\n", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Build(source));
    }

    [Fact]
    public void Build_RejectsMalformedFlowLinkEntry()
    {
        string source = BaseAsset().Replace("      slotIndex: 0", "      slotIndex: invalid", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Build(source));
    }

    private static VfxTypedGraph Build(string source) => VfxTypedGraph.Build(VfxYamlAsset.Parse(source));

    private static string BaseAsset()
        => Preamble +
           ModelDocument(10, GraphGuid, 0, new long[] { 20, 30 }) +
           ContextDocument(20, SpawnerGuid, 10, new long[] { 50 }, true) +
           ContextDocument(30, InitializeGuid, 10, Array.Empty<long>(), false) +
           BlockDocument() +
           SlotDocument() +
           "--- !u!2058629511 &90\nVisualEffectResource:\n  m_Graph: {fileID: 10}\n";

    private static string ModelDocument(long fileId, string guid, long parentId, IReadOnlyList<long> children)
        => $"--- !u!114 &{fileId}\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n" +
           $"  m_Name: Model{fileId}\n" +
           $"  m_Parent: {{fileID: {parentId}}}\n" +
           ReferenceList("m_Children", children) +
           "  m_InputSlots: []\n  m_OutputSlots: []\n";

    private static string ContextDocument(
        long fileId,
        string guid,
        long parentId,
        IReadOnlyList<long> children,
        bool source)
        => $"--- !u!114 &{fileId}\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n" +
           $"  m_Name: Context{fileId}\n" +
           $"  m_Parent: {{fileID: {parentId}}}\n" +
           ReferenceList("m_Children", children) +
           "  m_InputSlots: []\n  m_OutputSlots: []\n" +
           (source
               ? "  m_InputFlowSlot:\n  - link: []\n  - link: []\n" +
                 "  m_OutputFlowSlot:\n  - link:\n    - context: {fileID: 30}\n      slotIndex: 0\n"
               : "  m_InputFlowSlot:\n  - link:\n    - context: {fileID: 20}\n      slotIndex: 0\n" +
                 "  m_OutputFlowSlot:\n  - link: []\n");

    private static string BlockDocument()
        => "--- !u!114 &50\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {RateGuid}, type: 3}}\n" +
           "  m_Name: Rate\n  m_Parent: {fileID: 20}\n  m_Children: []\n" +
           "  m_InputSlots:\n  - {fileID: 60}\n  m_OutputSlots: []\n";

    private static string SlotDocument()
        => "--- !u!114 &60\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {FloatSlotGuid}, type: 3}}\n" +
           "  m_Name: Rate\n  m_Parent: {fileID: 0}\n  m_Children: []\n" +
           "  m_MasterSlot: {fileID: 60}\n  m_MasterData:\n    m_Owner: {fileID: 50}\n" +
           "    m_Value:\n      m_Type:\n        m_SerializableType: System.Single, mscorlib\n" +
           "      m_SerializableObject: 16\n    m_Space: 2147483647\n" +
           "  m_Property:\n    name: Rate\n    m_serializedType:\n" +
           "      m_SerializableType: System.Single, mscorlib\n" +
           "  m_Direction: 0\n  m_LinkedSlots: []\n";

    private static string DataDocument(long fileId, IReadOnlyList<long> owners)
        => $"--- !u!114 &{fileId}\nMonoBehaviour:\n" +
           "  m_Script: {fileID: 11500000, guid: d78581a96eae8bf4398c282eb0b098bd, type: 3}\n" +
           "  m_Name: VFXDataParticle\n  m_Parent: {fileID: 0}\n  m_Children: []\n" +
           ReferenceList("m_Owners", owners);

    private static string ReferenceList(string fieldName, IReadOnlyList<long> values)
    {
        if (values.Count == 0) return $"  {fieldName}: []\n";
        return $"  {fieldName}:\n" + string.Concat(values.Select(value => $"  - {{fileID: {value}}}\n"));
    }

    private const string Preamble = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";
    private const string GraphGuid = "7d4c867f6b72b714dbb5fd1780afe208";
    private const string SpawnerGuid = "73a13919d81fb7444849bae8b5c812a2";
    private const string InitializeGuid = "9dfea48843f53fc438eabc12a3a30abc";
    private const string RateGuid = "f05c6884b705ce14d82ae720f0ec209f";
    private const string FloatSlotGuid = "f780aa281814f9842a7c076d436932e7";
    private const string SpawnerSetAttributeGuid = "709ca816312218f4ba70763d893c34c9";
    private const string SpawnerCustomWrapperGuid = "4bfc68bea08ee074899e288b438a2e89";
    private const string UnknownGuid = "11111111111111111111111111111111";
}
