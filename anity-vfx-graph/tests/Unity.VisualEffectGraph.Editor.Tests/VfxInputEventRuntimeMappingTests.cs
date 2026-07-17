using System.Text;
using UnityEditor.VFX.Generation;
using UnityEditor.VFX.Model;
using UnityEditor.VFX.Serialization;
using UnityEngine;
using UnityEngine.VFX;
using Xunit;

namespace Unity.VisualEffectGraph.Editor.Tests;

public sealed class VfxInputEventRuntimeMappingTests
{
    [Fact]
    public void DirectEventToInitialize_CompilesParticleTargetWithoutSpawnerPath()
    {
        VFXRuntimeInputEventData input = CompileOne(
            new[] { Event(20, "Burst"), Init(40, 2000) },
            new[] { Edge(20, 40) },
            new[] { Particle(2000, "Sparks") });

        VFXRuntimeInputEventTargetData target = Assert.Single(input.Targets);
        Assert.Equal(40, target.InitializeContextId);
        Assert.Equal("Sparks", target.ParticleSystemName);
        Assert.Empty(target.SpawnerContextIds);
        Assert.Empty(target.SpawnSystemNames);
    }

    [Fact]
    public void EventThroughSpawner_CompilesContextAndSystemPath()
    {
        VFXRuntimeInputEventData input = CompileOne(
            new[] { Event(20, "Burst"), Spawner(30, 1000, "Rate"), Init(40, 2000) },
            new[] { Edge(20, 30), Edge(30, 40) },
            new[] { Particle(2000, "Sparks") });

        VFXRuntimeInputEventTargetData target = Assert.Single(input.Targets);
        Assert.Equal(new long[] { 30 }, target.SpawnerContextIds);
        Assert.Equal(new[] { "Rate" }, target.SpawnSystemNames);
    }

    [Fact]
    public void ChainedSpawners_PreserveFullGraphOrder()
    {
        VFXRuntimeInputEventData input = CompileOne(
            new[]
            {
                Event(20, "Burst"), Spawner(30, 1000, "First"),
                Spawner(31, 1001, "Second"), Init(40, 2000)
            },
            new[] { Edge(20, 30), Edge(30, 31), Edge(31, 40) },
            new[] { Particle(2000, "Sparks") });

        VFXRuntimeInputEventTargetData target = Assert.Single(input.Targets);
        Assert.Equal(new long[] { 30, 31 }, target.SpawnerContextIds);
        Assert.Equal(new[] { "First", "Second" }, target.SpawnSystemNames);
    }

    [Fact]
    public void BranchingEvent_CompilesEveryInitializeTargetInFlowOrder()
    {
        VFXRuntimeInputEventData input = CompileOne(
            new[]
            {
                Event(20, "Burst"), Spawner(30, 1000, "Left"), Init(40, 2000),
                Spawner(31, 1001, "Right"), Init(41, 2001)
            },
            new[] { Edge(20, 30), Edge(30, 40), Edge(20, 31), Edge(31, 41) },
            new[] { Particle(2000, "Sparks"), Particle(2001, "Smoke") });

        Assert.Equal(new long[] { 40, 41 }, input.Targets.Select(target => target.InitializeContextId));
        Assert.Equal(new[] { "Sparks", "Smoke" }, input.Targets.Select(target => target.ParticleSystemName));
    }

    [Fact]
    public void SameNameEventContexts_MergeDistinctTargets()
    {
        VFXRuntimeAssetData data = Compile(
            new[] { Event(20, "Burst"), Event(21, "Burst"), Init(40, 2000), Init(41, 2001) },
            new[] { Edge(20, 40), Edge(21, 41) },
            new[] { Particle(2000, "Sparks"), Particle(2001, "Smoke") });

        VFXRuntimeInputEventData input = Assert.Single(data.InputEventDispatches);
        Assert.Equal(new long[] { 40, 41 }, input.Targets.Select(target => target.InitializeContextId));
    }

    [Fact]
    public void UnconnectedEvent_RemainsPublicWithEmptyDispatchTargets()
    {
        VFXRuntimeInputEventData input = CompileOne(
            new[] { Event(20, "Manual") },
            Array.Empty<EdgeSpec>(),
            Array.Empty<ParticleSpec>());

        Assert.Equal("Manual", input.Name);
        Assert.Empty(input.Targets);
    }

    [Fact]
    public void DifferentEventNames_KeepIndependentTargetSets()
    {
        VFXRuntimeAssetData data = Compile(
            new[] { Event(20, "Burst"), Event(21, "Stop"), Init(40, 2000), Init(41, 2001) },
            new[] { Edge(20, 40), Edge(21, 41) },
            new[] { Particle(2000, "Sparks"), Particle(2001, "Smoke") });

        Assert.Equal(new[] { "Burst", "Stop" }, data.InputEventDispatches.Select(input => input.Name));
        Assert.Equal("Sparks", Assert.Single(data.InputEventDispatches[0].Targets).ParticleSystemName);
        Assert.Equal("Smoke", Assert.Single(data.InputEventDispatches[1].Targets).ParticleSystemName);
    }

    [Fact]
    public void RuntimeDataRoundTrip_PreservesDispatchPathsExactly()
    {
        VFXRuntimeAssetData compiled = Compile(
            new[] { Event(20, "Burst"), Spawner(30, 1000, "Rate"), Init(40, 2000) },
            new[] { Edge(20, 30), Edge(30, 40) },
            new[] { Particle(2000, "Sparks") });

        VFXRuntimeAssetData restored = VFXRuntimeAssetData.Deserialize(compiled.Serialize());

        VFXRuntimeInputEventData restoredInput = Assert.Single(restored.InputEventDispatches);
        Assert.Equal("Burst", restoredInput.Name);
        VFXRuntimeInputEventTargetData target = Assert.Single(restoredInput.Targets);
        Assert.Equal(40, target.InitializeContextId);
        Assert.Equal("Sparks", target.ParticleSystemName);
        Assert.Equal(new long[] { 30 }, target.SpawnerContextIds);
        Assert.Equal(new[] { "Rate" }, target.SpawnSystemNames);
    }

    [Fact]
    public void CompileInto_ImportsDispatchLookupByUnityPropertyId()
    {
        VfxTypedGraph graph = Graph(
            new[] { Event(20, "Burst"), Spawner(30, 1000, "Rate"), Init(40, 2000) },
            new[] { Edge(20, 30), Edge(30, 40) },
            new[] { Particle(2000, "Sparks") });
        var asset = new VisualEffectAsset();

        VfxRuntimeAssetCompiler.CompileInto(graph, asset);

        Assert.True(asset.TryGetInputEventRuntimeData(
            Shader.PropertyToID("Burst"), out VFXRuntimeInputEventData input));
        Assert.Equal("Sparks", Assert.Single(input.Targets).ParticleSystemName);
    }

    [Fact]
    public void MappingCompilation_IsByteDeterministic()
    {
        VfxTypedGraph graph = Graph(
            new[] { Event(20, "Burst"), Spawner(30, 1000, "Rate"), Init(40, 2000) },
            new[] { Edge(20, 30), Edge(30, 40) },
            new[] { Particle(2000, "Sparks") });

        Assert.Equal(VfxRuntimeAssetCompiler.Compile(graph), VfxRuntimeAssetCompiler.Compile(graph));
    }

    [Fact]
    public void RuntimeDataRejectsUnknownParticleTarget()
    {
        var input = new VFXRuntimeInputEventData("Burst", new[]
        {
            new VFXRuntimeInputEventTargetData(40, "Missing", Array.Empty<long>(), Array.Empty<string>())
        });
        var data = new VFXRuntimeAssetData(
            Array.Empty<VFXRuntimeAttributeData>(), new[] { "Burst" }, new[] { input },
            Array.Empty<VFXRuntimeSystemData>(), Array.Empty<VFXRuntimeOutputEventData>());

        Assert.Throws<InvalidDataException>(data.Serialize);
    }

    [Fact]
    public void RuntimeDataRejectsMismatchedSpawnerPathLists()
    {
        var input = new VFXRuntimeInputEventData("Burst", new[]
        {
            new VFXRuntimeInputEventTargetData(40, "Sparks", new long[] { 30 }, Array.Empty<string>())
        });
        var data = new VFXRuntimeAssetData(
            Array.Empty<VFXRuntimeAttributeData>(), new[] { "Burst" }, new[] { input },
            new[] { new VFXRuntimeSystemData("Sparks", VFXRuntimeSystemKind.Particle, 64) },
            Array.Empty<VFXRuntimeOutputEventData>());

        Assert.Throws<InvalidDataException>(data.Serialize);
    }

    private static VFXRuntimeInputEventData CompileOne(
        IReadOnlyList<ContextSpec> contexts,
        IReadOnlyList<EdgeSpec> edges,
        IReadOnlyList<ParticleSpec> particles)
        => Assert.Single(Compile(contexts, edges, particles).InputEventDispatches);

    private static VFXRuntimeAssetData Compile(
        IReadOnlyList<ContextSpec> contexts,
        IReadOnlyList<EdgeSpec> edges,
        IReadOnlyList<ParticleSpec> particles)
        => VFXRuntimeAssetData.Deserialize(
            VfxRuntimeAssetCompiler.Compile(Graph(contexts, edges, particles)));

    private static VfxTypedGraph Graph(
        IReadOnlyList<ContextSpec> contexts,
        IReadOnlyList<EdgeSpec> edges,
        IReadOnlyList<ParticleSpec> particles)
    {
        var source = new StringBuilder(Preamble)
            .Append("--- !u!114 &10\nMonoBehaviour:\n")
            .Append("  m_Script: {fileID: 11500000, guid: ").Append(GraphGuid).Append(", type: 3}\n")
            .Append("  m_Name: Graph\n  m_Parent: {fileID: 0}\n")
            .Append(References("m_Children", contexts.Select(context => context.Id)))
            .Append("  m_InputSlots: []\n  m_OutputSlots: []\n");
        foreach (ContextSpec context in contexts) source.Append(Context(context, edges));
        foreach (ContextSpec context in contexts.Where(context => context.Kind == ContextKind.Spawner))
            source.Append(SpawnerData(context.DataId, context.Id));
        foreach (ParticleSpec particle in particles)
            source.Append(ParticleData(particle, contexts.Where(context => context.DataId == particle.Id)
                .Select(context => context.Id)));
        source.Append("--- !u!2058629511 &9000\nVisualEffectResource:\n  m_Graph: {fileID: 10}\n");
        return VfxTypedGraph.Build(VfxYamlAsset.Parse(source.ToString()));
    }

    private static string Context(ContextSpec context, IReadOnlyList<EdgeSpec> edges)
    {
        string guid = context.Kind switch
        {
            ContextKind.Event => EventGuid,
            ContextKind.Spawner => SpawnerGuid,
            ContextKind.Init => InitGuid,
            _ => throw new ArgumentOutOfRangeException()
        };
        int inputCount = context.Kind == ContextKind.Spawner ? 2 : 1;
        var source = new StringBuilder()
            .Append("--- !u!114 &").Append(context.Id).Append("\nMonoBehaviour:\n")
            .Append("  m_Script: {fileID: 11500000, guid: ").Append(guid).Append(", type: 3}\n")
            .Append("  m_Name: Context").Append(context.Id).Append("\n  m_Parent: {fileID: 10}\n")
            .Append("  m_Children: []\n  m_InputSlots: []\n  m_OutputSlots: []\n");
        if (context.DataId != 0) source.Append("  m_Data: {fileID: ").Append(context.DataId).Append("}\n");
        source.Append(FlowSlots("m_InputFlowSlot", inputCount, edges, context.Id, true))
            .Append(FlowSlots("m_OutputFlowSlot", 1, edges, context.Id, false));
        if (context.EventName is not null) source.Append("  eventName: ").Append(context.EventName).Append('\n');
        if (context.Label is not null) source.Append("  m_Label: ").Append(context.Label).Append('\n');
        return source.ToString();
    }

    private static string FlowSlots(
        string field, int count, IReadOnlyList<EdgeSpec> edges, long contextId, bool incoming)
    {
        var source = new StringBuilder("  ").Append(field).Append(":\n");
        for (int slot = 0; slot < count; slot++)
        {
            EdgeSpec[] links = edges.Where(edge => incoming
                ? edge.Target == contextId
                : edge.Source == contextId).ToArray();
            if (slot != 0 || links.Length == 0)
            {
                source.Append("  - link: []\n");
                continue;
            }
            source.Append("  - link:\n");
            foreach (EdgeSpec edge in links)
                source.Append("    - context: {fileID: ")
                    .Append(incoming ? edge.Source : edge.Target)
                    .Append("}\n      slotIndex: 0\n");
        }
        return source.ToString();
    }

    private static string SpawnerData(long dataId, long ownerId)
        => "--- !u!114 &" + dataId + "\nMonoBehaviour:\n" +
           "  m_Script: {fileID: 11500000, guid: " + SpawnerDataGuid + ", type: 3}\n" +
           "  m_Name: SpawnerData\n  m_Parent: {fileID: 0}\n  m_Children: []\n" +
           "  m_Owners:\n  - {fileID: " + ownerId + "}\n";

    private static string ParticleData(ParticleSpec particle, IEnumerable<long> owners)
        => "--- !u!114 &" + particle.Id + "\nMonoBehaviour:\n" +
           "  m_Script: {fileID: 11500000, guid: " + ParticleDataGuid + ", type: 3}\n" +
           "  m_Name: ParticleData\n  m_Parent: {fileID: 0}\n  m_Children: []\n" +
           References("m_Owners", owners) +
           "  title: " + particle.Title + "\n  dataType: 0\n  capacity: 1024\n" +
           "  stripCapacity: 1\n  particlePerStripCount: 1\n" +
           "  needsComputeBounds: 0\n  boundsMode: 0\n  m_Space: 0\n";

    private static string References(string field, IEnumerable<long> values)
    {
        long[] array = values.ToArray();
        return array.Length == 0
            ? "  " + field + ": []\n"
            : "  " + field + ":\n" + string.Concat(array.Select(value => "  - {fileID: " + value + "}\n"));
    }

    private static ContextSpec Event(long id, string name) => new(id, ContextKind.Event, name, 0, null);
    private static ContextSpec Spawner(long id, long dataId, string label) => new(id, ContextKind.Spawner, null, dataId, label);
    private static ContextSpec Init(long id, long dataId) => new(id, ContextKind.Init, null, dataId, null);
    private static ParticleSpec Particle(long id, string title) => new(id, title);
    private static EdgeSpec Edge(long source, long target) => new(source, target);

    private sealed record ContextSpec(long Id, ContextKind Kind, string? EventName, long DataId, string? Label);
    private sealed record ParticleSpec(long Id, string Title);
    private sealed record EdgeSpec(long Source, long Target);
    private enum ContextKind { Event, Spawner, Init }

    private const string Preamble = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";
    private const string GraphGuid = "7d4c867f6b72b714dbb5fd1780afe208";
    private const string EventGuid = "2461f61b3c026d54db1951a4e17ab20e";
    private const string SpawnerGuid = "73a13919d81fb7444849bae8b5c812a2";
    private const string InitGuid = "9dfea48843f53fc438eabc12a3a30abc";
    private const string SpawnerDataGuid = "f68759077adc0b143b6e1c101e82065e";
    private const string ParticleDataGuid = "d78581a96eae8bf4398c282eb0b098bd";
}
