using System.Text;
using UnityEditor.VFX.Generation;
using UnityEditor.VFX.Model;
using UnityEditor.VFX.Serialization;
using UnityEngine.VFX;
using Xunit;

namespace Unity.VisualEffectGraph.Editor.Tests;

public sealed class VfxOutputEventCompilerTests
{
    [Fact]
    public void OutputEvent_CompilesAsCpuDescriptorWithoutShaderOrGpuLayout()
    {
        VfxContextKernelCompilation result = Compile(
            new[] { Spawner(20), Output(30, "Hit") },
            new[] { Edge(20, 30) },
            30);

        Assert.Equal(VfxKernelStage.Cpu, result.Stage);
        Assert.Empty(result.HlslSource);
        Assert.Equal(0, result.ThreadGroupSize);
        Assert.Empty(result.AttributeLayout);
        Assert.Empty(result.SourceAttributeLayout);
        Assert.Null(result.VertexEntryPoint);
        Assert.Null(result.FragmentEntryPoint);
    }

    [Fact]
    public void OutputEvent_PreservesEventNameAndDisablesInstancing()
    {
        VfxContextKernelCompilation result = Compile(
            new[] { Spawner(20), Output(30, "Enemy Defeated") },
            new[] { Edge(20, 30) },
            30);

        Assert.Equal("Enemy Defeated", result.OutputEventName);
        Assert.True(result.DisablesInstancing);
        Assert.Equal(new long[] { 30 }, result.OutputEventContextIds);
    }

    [Fact]
    public void OutputEvent_MapsEveryDirectSpawnerAsSpawnerInput()
    {
        VfxContextKernelCompilation result = Compile(
            new[] { Spawner(20), Spawner(21), Output(30, "Hit") },
            new[] { Edge(20, 30), Edge(21, 30) },
            30);

        Assert.Equal(new long[] { 20, 21 },
            result.OutputEventBufferMappings.Select(mapping => mapping.SourceSpawnerContextId));
        Assert.All(result.OutputEventBufferMappings,
            mapping => Assert.Equal("spawner_input", mapping.Name));
    }

    [Fact]
    public void OutputEvent_WithoutInputIsNotCompilable()
        => Assert.Throws<InvalidOperationException>(() => Compile(
            new[] { Output(30, "Hit") },
            Array.Empty<FlowEdgeSpec>(),
            30));

    [Fact]
    public void OutputEvent_RejectsDirectCpuEventInputLikeUnityLinkRules()
        => Assert.Throws<InvalidOperationException>(() => Compile(
            new[] { Event(20, "OnPlay"), Output(30, "Hit") },
            new[] { Edge(20, 30) },
            30));

    [Fact]
    public void OutputEvent_SameNameContextsMergeIntoOneSystemContract()
    {
        VfxContextKernelCompilation result = Compile(
            new[] { Spawner(20), Spawner(21), Output(30, "Hit"), Output(31, "Hit") },
            new[] { Edge(20, 30), Edge(21, 31) },
            30);

        Assert.Equal(new long[] { 30, 31 }, result.OutputEventContextIds);
        Assert.Equal(new long[] { 20, 21 },
            result.OutputEventBufferMappings.Select(mapping => mapping.SourceSpawnerContextId));
    }

    [Fact]
    public void OutputEvent_DifferentNamesRemainSeparateSystems()
    {
        ContextSpec[] contexts = { Spawner(20), Spawner(21), Output(30, "Hit"), Output(31, "Heal") };
        FlowEdgeSpec[] edges = { Edge(20, 30), Edge(21, 31) };

        VfxContextKernelCompilation hit = Compile(contexts, edges, 30);
        VfxContextKernelCompilation heal = Compile(contexts, edges, 31);

        Assert.Equal(new long[] { 30 }, hit.OutputEventContextIds);
        Assert.Equal(new long[] { 20 }, hit.OutputEventBufferMappings.Select(mapping => mapping.SourceSpawnerContextId));
        Assert.Equal(new long[] { 31 }, heal.OutputEventContextIds);
        Assert.Equal(new long[] { 21 }, heal.OutputEventBufferMappings.Select(mapping => mapping.SourceSpawnerContextId));
    }

    [Fact]
    public void OutputEvent_DeduplicatesSpawnerMappedThroughSameNameContexts()
    {
        VfxContextKernelCompilation result = Compile(
            new[] { Spawner(20), Output(30, "Hit"), Output(31, "Hit") },
            new[] { Edge(20, 30), Edge(20, 31) },
            31);

        VfxOutputEventBufferMapping mapping = Assert.Single(result.OutputEventBufferMappings);
        Assert.Equal(20, mapping.SourceSpawnerContextId);
    }

    [Fact]
    public void OutputEvent_CollectsSpawnerSetAttributeAsReadSourceContract()
    {
        VfxContextKernelCompilation result = Compile(
            new[] { Spawner(20, "position"), Output(30, "Hit") },
            new[] { Edge(20, 30) },
            30);

        VfxAttributeDefinition attribute = Assert.Single(result.OutputEventAttributes);
        Assert.Equal("position", attribute.Name);
        Assert.Equal("float3", attribute.HlslType);
        Assert.Equal(VfxAttributeMode.ReadSource, result.OutputEventAttributeMode);
        Assert.Single(result.CompiledBlockIds);
    }

    [Fact]
    public void OutputEvent_AttributeContractUsesCatalogOrder()
    {
        VfxContextKernelCompilation result = Compile(
            new[] { Spawner(20, "size", "position", "color"), Output(30, "Hit") },
            new[] { Edge(20, 30) },
            30);

        Assert.Equal(new[] { "position", "color", "size" },
            result.OutputEventAttributes.Select(attribute => attribute.Name));
    }

    [Fact]
    public void OutputEvent_DeduplicatesRepeatedSourceAttributes()
    {
        VfxContextKernelCompilation result = Compile(
            new[] { Spawner(20, "position", "position"), Output(30, "Hit") },
            new[] { Edge(20, 30) },
            30);

        Assert.Equal(new[] { "position" },
            result.OutputEventAttributes.Select(attribute => attribute.Name));
        Assert.Equal(2, result.CompiledBlockIds.Count);
    }

    [Fact]
    public void OutputEvent_RecursivelyCollectsUpstreamSpawnerAttributes()
    {
        VfxContextKernelCompilation result = Compile(
            new[] { Spawner(20, "color"), Spawner(21, "size"), Output(30, "Hit") },
            new[] { Edge(20, 21), Edge(21, 30) },
            30);

        Assert.Equal(new long[] { 21 },
            result.OutputEventBufferMappings.Select(mapping => mapping.SourceSpawnerContextId));
        Assert.Equal(new[] { "color", "size" },
            result.OutputEventAttributes.Select(attribute => attribute.Name));
    }

    [Theory]
    [InlineData("spawnTime")]
    [InlineData("spawnCount")]
    public void OutputEvent_ExportsSpawnOnlyAttributes(string attributeName)
    {
        VfxContextKernelCompilation result = Compile(
            new[] { Spawner(20, attributeName), Output(30, "Hit") },
            new[] { Edge(20, 30) },
            30);

        Assert.Equal(attributeName, Assert.Single(result.OutputEventAttributes).Name);
    }

    [Fact]
    public void OutputEvent_WithNoSetAttributeHasEmptySourceContract()
    {
        VfxContextKernelCompilation result = Compile(
            new[] { Spawner(20), Output(30, "Hit") },
            new[] { Edge(20, 30) },
            30);

        Assert.Empty(result.OutputEventAttributes);
        Assert.Equal(VfxAttributeMode.ReadSource, result.OutputEventAttributeMode);
        Assert.Empty(result.CompiledBlockIds);
        Assert.False(result.UsesSource);
        Assert.False(result.UsesExternalSourceBuffer);
    }

    [Fact]
    public void OutputEvent_RecordLayoutPrependsSpawnCountToReadSourceAttributes()
    {
        VfxContextKernelCompilation result = Compile(
            new[] { Spawner(20, "position", "size"), Output(30, "Hit") },
            new[] { Edge(20, 30) },
            30);

        VfxEventAttributeLayout layout = result.CreateOutputEventRecordLayout();

        Assert.Equal(new[] { "spawnCount", "position", "size" },
            layout.Fields.Select(field => field.Name));
        Assert.Equal(5, layout.StructureSizeWords);
    }

    [Fact]
    public void NonOutputCompilationCannotCreateOutputEventRecordLayout()
    {
        VfxContextKernelCompilation result = new(
            1,
            string.Empty,
            Array.Empty<long>(),
            false,
            false,
            false,
            false,
            false,
            Array.Empty<VfxAttributeDefinition>(),
            Array.Empty<VfxAttributeDefinition>(),
            64);

        Assert.Throws<InvalidOperationException>(() => result.CreateOutputEventRecordLayout());
    }

    [Fact]
    public void RuntimeAssetCompiler_IsByteDeterministic()
    {
        VfxTypedGraph graph = BuildGraph(
            new[] { Spawner(20, "position"), Output(30, "Hit") },
            new[] { Edge(20, 30) });

        Assert.Equal(VfxRuntimeAssetCompiler.Compile(graph), VfxRuntimeAssetCompiler.Compile(graph));
    }

    [Fact]
    public void RuntimeAssetCompiler_ImportsCpuInputEventNamesInGraphOrder()
    {
        VfxTypedGraph graph = BuildGraph(
            new[] { Event(40, "Custom Start"), Event(41, "Custom Stop") },
            Array.Empty<FlowEdgeSpec>());
        var asset = new VisualEffectAsset();

        VfxRuntimeAssetCompiler.CompileInto(graph, asset);

        var names = new List<string>();
        asset.GetEvents(names);
        Assert.Equal(new[] { "Custom Start", "Custom Stop" }, names);
    }

    [Fact]
    public void RuntimeAssetCompiler_DeduplicatesCpuInputEventNames()
    {
        VfxTypedGraph graph = BuildGraph(
            new[] { Event(40, "Wake"), Event(41, "Wake") },
            Array.Empty<FlowEdgeSpec>());
        var asset = new VisualEffectAsset();

        VfxRuntimeAssetCompiler.CompileInto(graph, asset);

        var names = new List<string>();
        asset.GetEvents(names);
        Assert.Equal(new[] { "Wake" }, names);
    }

    [Fact]
    public void RuntimeAssetCompiler_ImportsSpawnerSystemsAndDefaultState()
    {
        VfxTypedGraph graph = BuildGraph(
            new[] { Spawner(20), Spawner(21) },
            Array.Empty<FlowEdgeSpec>());
        var asset = new VisualEffectAsset();
        VfxRuntimeAssetCompiler.CompileInto(graph, asset);
        var effect = new VisualEffect { visualEffectAsset = asset };

        var names = new List<string>();
        effect.GetSpawnSystemNames(names);

        Assert.Equal(new[] { "Context20", "Context21" }, names);
        using VFXSpawnerState state = effect.GetSpawnSystemInfo("Context20");
        Assert.False(state.playing);
        Assert.Equal(0f, state.spawnCount);
    }

    [Fact]
    public void RuntimeAssetCompiler_ImportsParticleCapacityAndUnityStyleSystemName()
    {
        VfxTypedGraph graph = BuildGraphWithParticleData("Swarm (12)", 4096);
        var asset = new VisualEffectAsset();
        VfxRuntimeAssetCompiler.CompileInto(graph, asset);
        var effect = new VisualEffect { visualEffectAsset = asset };

        var names = new List<string>();
        effect.GetParticleSystemNames(names);

        Assert.Equal(new[] { "Swarm" }, names);
        VFXParticleSystemInfo info = effect.GetParticleSystemInfo("Swarm");
        Assert.Equal(4096u, info.capacity);
        Assert.Equal(0u, info.aliveCount);
        Assert.True(info.sleeping);
    }

    [Fact]
    public void RuntimeAssetCompiler_ImportsOutputEventSystemName()
    {
        VfxTypedGraph graph = BuildGraph(
            new[] { Spawner(20), Output(30, "Enemy Hit") },
            new[] { Edge(20, 30) });
        var asset = new VisualEffectAsset();
        VfxRuntimeAssetCompiler.CompileInto(graph, asset);
        var effect = new VisualEffect { visualEffectAsset = asset };

        var names = new List<string>();
        effect.GetOutputEventNames(names);

        Assert.Equal(new[] { "Enemy Hit" }, names);
        Assert.True(effect.HasSystem("Enemy Hit"));
    }

    [Fact]
    public void RuntimeAssetCompiler_ImportsOutputContextAndSpawnerMappings()
    {
        VfxTypedGraph graph = BuildGraph(
            new[] { Spawner(20), Spawner(21), Output(30, "Hit"), Output(31, "Hit") },
            new[] { Edge(20, 30), Edge(21, 31) });
        var asset = new VisualEffectAsset();
        VfxRuntimeAssetCompiler.CompileInto(graph, asset);

        Assert.True(asset.TryGetOutputEventRuntimeData(UnityEngine.Shader.PropertyToID("Hit"), out VFXRuntimeOutputEventData data));
        Assert.Equal(new long[] { 30, 31 }, data.ContextIds);
        Assert.Equal(new long[] { 20, 21 }, data.BufferMappings.Select(mapping => mapping.SourceSpawnerContextId));
        Assert.All(data.BufferMappings, mapping => Assert.Equal("spawner_input", mapping.Name));
    }

    [Fact]
    public void RuntimeAssetCompiler_ImportsOutputRecordLayoutExactly()
    {
        VfxTypedGraph graph = BuildGraph(
            new[] { Spawner(20, "position", "size"), Output(30, "Hit") },
            new[] { Edge(20, 30) });
        var asset = new VisualEffectAsset();
        VfxRuntimeAssetCompiler.CompileInto(graph, asset);

        Assert.True(asset.TryGetOutputEventRuntimeData(UnityEngine.Shader.PropertyToID("Hit"), out VFXRuntimeOutputEventData data));
        Assert.Equal(new[] { "spawnCount", "position", "size" }, data.Attributes.Select(attribute => attribute.Name));
        Assert.Equal(new[] { 0, 1, 4 }, data.Attributes.Select(attribute => attribute.OffsetWords));
        Assert.Equal(5, data.StrideWords);
    }

    [Fact]
    public void RuntimeAssetCompiler_OutputAttributesBecomeEventAttributeSchema()
    {
        VfxTypedGraph graph = BuildGraph(
            new[] { Spawner(20, "position", "size"), Output(30, "Hit") },
            new[] { Edge(20, 30) });
        var asset = new VisualEffectAsset();
        VfxRuntimeAssetCompiler.CompileInto(graph, asset);

        using VFXEventAttribute attribute = new(asset);
        Assert.True(attribute.HasFloat("spawnCount"));
        Assert.True(attribute.HasVector3("position"));
        Assert.True(attribute.HasFloat("size"));
        Assert.False(attribute.HasUint("position"));
    }

    [Fact]
    public void RuntimeAssetCompiler_ImportReplacesPreviousRuntimeState()
    {
        var asset = new VisualEffectAsset();
        VfxRuntimeAssetCompiler.CompileInto(BuildGraph(
            new[] { Spawner(20), Output(30, "First") },
            new[] { Edge(20, 30) }), asset);

        VfxRuntimeAssetCompiler.CompileInto(BuildGraph(
            new[] { Event(40, "Second") },
            Array.Empty<FlowEdgeSpec>()), asset);

        var outputNames = new List<string>();
        asset.GetOutputEventNames(outputNames);
        var eventNames = new List<string>();
        asset.GetEvents(eventNames);
        Assert.Empty(outputNames);
        Assert.Equal(new[] { "Second" }, eventNames);
        Assert.False(asset.HasSystem(UnityEngine.Shader.PropertyToID("Context20")));
    }

    [Fact]
    public void RuntimeAssetCompiler_ChecksumCorruptionIsRejectedAtomically()
    {
        var asset = new VisualEffectAsset();
        VfxRuntimeAssetCompiler.CompileInto(BuildGraph(
            new[] { Event(40, "Stable") },
            Array.Empty<FlowEdgeSpec>()), asset);
        uint version = asset.GetCompilationVersion();
        byte[] corrupted = VfxRuntimeAssetCompiler.Compile(BuildGraph(
            new[] { Event(41, "Replacement") },
            Array.Empty<FlowEdgeSpec>()));
        corrupted[^1] ^= 0x80;

        Assert.Throws<InvalidDataException>(() => asset.ImportRuntimeData(corrupted));

        var names = new List<string>();
        asset.GetEvents(names);
        Assert.Equal(new[] { "Stable" }, names);
        Assert.Equal(version, asset.GetCompilationVersion());
    }

    [Fact]
    public void RuntimeAssetCompiler_TruncatedEnvelopeIsRejected()
    {
        byte[] bytes = VfxRuntimeAssetCompiler.Compile(BuildGraph(
            new[] { Event(40, "Wake") },
            Array.Empty<FlowEdgeSpec>()));

        Assert.Throws<InvalidDataException>(() => VFXRuntimeAssetData.Deserialize(bytes.AsSpan(0, bytes.Length - 1)));
    }

    [Fact]
    public void RuntimeAssetCompiler_TrailingEnvelopeBytesAreRejected()
    {
        byte[] bytes = VfxRuntimeAssetCompiler.Compile(BuildGraph(
            new[] { Event(40, "Wake") },
            Array.Empty<FlowEdgeSpec>()));
        Array.Resize(ref bytes, bytes.Length + 1);

        Assert.Throws<InvalidDataException>(() => VFXRuntimeAssetData.Deserialize(bytes));
    }

    [Fact]
    public void RuntimeAssetData_RejectsConflictingDuplicateAttributesBeforeSerialization()
    {
        var data = new VFXRuntimeAssetData(
            new[]
            {
                new VFXRuntimeAttributeData("value", VFXRuntimeValueType.Float, 0, 1),
                new VFXRuntimeAttributeData("value", VFXRuntimeValueType.UInt32, 1, 1)
            },
            Array.Empty<string>(),
            Array.Empty<VFXRuntimeInputEventData>(),
            Array.Empty<VFXRuntimeSystemData>(),
            Array.Empty<VFXRuntimeOutputEventData>());

        Assert.Throws<InvalidDataException>(data.Serialize);
    }

    [Fact]
    public void RuntimeAssetData_RejectsOutputLayoutPastStride()
    {
        var data = new VFXRuntimeAssetData(
            Array.Empty<VFXRuntimeAttributeData>(),
            Array.Empty<string>(),
            Array.Empty<VFXRuntimeInputEventData>(),
            Array.Empty<VFXRuntimeSystemData>(),
            new[]
            {
                new VFXRuntimeOutputEventData(
                    "Hit",
                    new long[] { 1 },
                    Array.Empty<VFXRuntimeOutputEventMapping>(),
                    new[] { new VFXRuntimeAttributeData("position", VFXRuntimeValueType.Float3, 0, 3) },
                    2)
            });

        Assert.Throws<InvalidDataException>(data.Serialize);
    }

    private static VfxContextKernelCompilation Compile(
        IReadOnlyList<ContextSpec> contexts,
        IReadOnlyList<FlowEdgeSpec> edges,
        long outputContextId)
    {
        VfxTypedGraph graph = VfxTypedGraph.Build(VfxYamlAsset.Parse(Graph(contexts, edges)));
        return VfxContextKernelCompiler.Compile(graph, outputContextId);
    }

    private static VfxTypedGraph BuildGraph(
        IReadOnlyList<ContextSpec> contexts,
        IReadOnlyList<FlowEdgeSpec> edges)
        => VfxTypedGraph.Build(VfxYamlAsset.Parse(Graph(contexts, edges)));

    private static VfxTypedGraph BuildGraphWithParticleData(string title, uint capacity)
    {
        string source = Graph(Array.Empty<ContextSpec>(), Array.Empty<FlowEdgeSpec>()) +
            "--- !u!114 &2000\nMonoBehaviour:\n" +
            "  m_Script: {fileID: 11500000, guid: d78581a96eae8bf4398c282eb0b098bd, type: 3}\n" +
            "  m_Name: ParticleData\n  m_Parent: {fileID: 0}\n  m_Children: []\n  m_Owners: []\n" +
            "  title: " + title + "\n" +
            "  dataType: 0\n  capacity: " + capacity + "\n  stripCapacity: 1\n" +
            "  particlePerStripCount: 1\n  needsComputeBounds: 0\n  boundsMode: 0\n  m_Space: 0\n";
        return VfxTypedGraph.Build(VfxYamlAsset.Parse(source));
    }

    private static string Graph(IReadOnlyList<ContextSpec> contexts, IReadOnlyList<FlowEdgeSpec> edges)
    {
        var source = new StringBuilder(Preamble);
        source.Append("--- !u!114 &10\nMonoBehaviour:\n")
            .Append("  m_Script: {fileID: 11500000, guid: ").Append(GraphGuid).Append(", type: 3}\n")
            .Append("  m_Name: Graph\n  m_Parent: {fileID: 0}\n")
            .Append(References("m_Children", contexts.Select(context => context.Id)))
            .Append("  m_InputSlots: []\n  m_OutputSlots: []\n");
        foreach (ContextSpec context in contexts)
            source.Append(Context(context, edges));
        foreach (ContextSpec context in contexts.Where(context => context.Kind == ContextKind.Spawner))
        {
            for (int index = 0; index < context.Attributes.Count; index++)
                source.Append(SpawnerSetAttribute(context.Id * 100 + index, context.Id, context.Attributes[index]));
            source.Append(SpawnerData(context));
        }
        source.Append("--- !u!2058629511 &9000\nVisualEffectResource:\n  m_Graph: {fileID: 10}\n");
        return source.ToString();
    }

    private static string Context(ContextSpec context, IReadOnlyList<FlowEdgeSpec> edges)
    {
        string guid = context.Kind switch
        {
            ContextKind.Spawner => SpawnerGuid,
            ContextKind.OutputEvent => OutputEventGuid,
            ContextKind.Event => EventGuid,
            _ => throw new ArgumentOutOfRangeException()
        };
        int inputCount = context.Kind == ContextKind.Spawner ? 2 : 1;
        int outputCount = context.Kind == ContextKind.OutputEvent ? 0 : 1;
        var source = new StringBuilder()
            .Append("--- !u!114 &").Append(context.Id).Append("\nMonoBehaviour:\n")
            .Append("  m_Script: {fileID: 11500000, guid: ").Append(guid).Append(", type: 3}\n")
            .Append("  m_Name: Context").Append(context.Id).Append("\n  m_Parent: {fileID: 10}\n")
            .Append(References("m_Children", Enumerable.Range(0, context.Attributes.Count)
                .Select(index => context.Id * 100 + index)))
            .Append("  m_InputSlots: []\n  m_OutputSlots: []\n");
        if (context.Kind == ContextKind.Spawner)
            source.Append("  m_Data: {fileID: ").Append(1000 + context.Id).Append("}\n");
        source.Append(FlowSlots("m_InputFlowSlot", inputCount, edges, context.Id, incoming: true))
            .Append(FlowSlots("m_OutputFlowSlot", outputCount, edges, context.Id, incoming: false));
        if (context.EventName is not null)
            source.Append("  eventName: ").Append(context.EventName).Append('\n');
        return source.ToString();
    }

    private static string FlowSlots(
        string field,
        int count,
        IReadOnlyList<FlowEdgeSpec> edges,
        long contextId,
        bool incoming)
    {
        if (count == 0) return "  " + field + ": []\n";
        var source = new StringBuilder("  ").Append(field).Append(":\n");
        for (int slot = 0; slot < count; slot++)
        {
            FlowEdgeSpec[] links = edges.Where(edge =>
                    incoming ? edge.Target == contextId : edge.Source == contextId)
                .ToArray();
            if (slot != 0 || links.Length == 0)
            {
                source.Append("  - link: []\n");
                continue;
            }
            source.Append("  - link:\n");
            foreach (FlowEdgeSpec link in links)
                source.Append("    - context: {fileID: ")
                    .Append(incoming ? link.Source : link.Target)
                    .Append("}\n      slotIndex: 0\n");
        }
        return source.ToString();
    }

    private static string SpawnerSetAttribute(long id, long parentId, string attribute)
        => "--- !u!114 &" + id + "\nMonoBehaviour:\n" +
           "  m_Script: {fileID: 11500000, guid: " + SpawnerSetAttributeGuid + ", type: 3}\n" +
           "  m_Name: Set SpawnEvent Attribute\n  m_Parent: {fileID: " + parentId + "}\n" +
           "  m_Children: []\n  m_InputSlots: []\n  m_OutputSlots: []\n" +
           "  m_Disabled: 0\n  attribute: " + attribute + "\n  randomMode: 0\n";

    private static string SpawnerData(ContextSpec context)
        => "--- !u!114 &" + (1000 + context.Id) + "\nMonoBehaviour:\n" +
           "  m_Script: {fileID: 11500000, guid: " + SpawnerDataGuid + ", type: 3}\n" +
           "  m_Name: SpawnerData\n  m_Parent: {fileID: 0}\n  m_Children: []\n" +
           "  m_Owners:\n  - {fileID: " + context.Id + "}\n";

    private static string References(string field, IEnumerable<long> values)
    {
        long[] array = values.ToArray();
        return array.Length == 0
            ? "  " + field + ": []\n"
            : "  " + field + ":\n" + string.Concat(array.Select(value => "  - {fileID: " + value + "}\n"));
    }

    private static ContextSpec Spawner(long id, params string[] attributes)
        => new(id, ContextKind.Spawner, null, attributes);

    private static ContextSpec Output(long id, string eventName)
        => new(id, ContextKind.OutputEvent, eventName, Array.Empty<string>());

    private static ContextSpec Event(long id, string eventName)
        => new(id, ContextKind.Event, eventName, Array.Empty<string>());

    private static FlowEdgeSpec Edge(long source, long target) => new(source, target);

    private sealed record ContextSpec(
        long Id,
        ContextKind Kind,
        string? EventName,
        IReadOnlyList<string> Attributes);

    private sealed record FlowEdgeSpec(long Source, long Target);

    private enum ContextKind { Spawner, OutputEvent, Event }

    private const string Preamble = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";
    private const string GraphGuid = "7d4c867f6b72b714dbb5fd1780afe208";
    private const string SpawnerGuid = "73a13919d81fb7444849bae8b5c812a2";
    private const string OutputEventGuid = "4f39de6f4fce95c4d9240e5055b057a6";
    private const string EventGuid = "2461f61b3c026d54db1951a4e17ab20e";
    private const string SpawnerDataGuid = "f68759077adc0b143b6e1c101e82065e";
    private const string SpawnerSetAttributeGuid = "709ca816312218f4ba70763d893c34c9";
}
