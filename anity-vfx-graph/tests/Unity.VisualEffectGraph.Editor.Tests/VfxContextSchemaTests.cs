using UnityEditor.VFX.Model;
using UnityEditor.VFX.Serialization;
using Xunit;

namespace Unity.VisualEffectGraph.Editor.Tests;

public sealed class VfxContextSchemaTests
{
    [Fact]
    public void ContextAndDataFlags_MatchUnity14Values()
    {
        Assert.Equal(1, (int)VfxContextType.Spawner);
        Assert.Equal(2, (int)VfxContextType.Init);
        Assert.Equal(8, (int)VfxContextType.Update);
        Assert.Equal(16, (int)VfxContextType.Output);
        Assert.Equal(64, (int)VfxContextType.SpawnerGpu);
        Assert.Equal(4, (int)VfxDataType.Particle);
        Assert.Equal(20, (int)VfxDataType.ParticleStrip);
    }

    [Fact]
    public void ParticleData_ParsesCapacityBoundsAndSimulationSpace()
    {
        VfxDataDescriptor data = Assert.Single(ParseData(ParticleData(80, Array.Empty<long>(), 0, 128, 16, 16, 1, 2, 1)));

        Assert.Equal(VfxDataType.Particle, data.DataType);
        Assert.Equal((uint)128, data.Capacity);
        Assert.Equal(VfxCoordinateSpace.World, data.Space);
        Assert.Equal(VfxBoundsSettingMode.Automatic, data.BoundsMode);
        Assert.True(data.NeedsComputeBounds);
    }

    [Fact]
    public void ParticleStripData_RequiresExactCapacityProduct()
    {
        VfxDataDescriptor data = Assert.Single(ParseData(ParticleData(80, Array.Empty<long>(), 1, 2880, 32, 90)));

        Assert.Equal(VfxDataType.ParticleStrip, data.DataType);
        Assert.Equal((uint)32, data.StripCapacity);
        Assert.Equal((uint)90, data.ParticlesPerStrip);
    }

    [Fact]
    public void ParticleStripData_InvalidCapacityProductIsRejected()
        => Assert.Throws<InvalidDataException>(() => ParseData(ParticleData(80, Array.Empty<long>(), 1, 100, 4, 20)));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ParticleData_NonPositiveCapacityIsRejected(int capacity)
        => Assert.Throws<InvalidDataException>(() => ParseData(ParticleData(80, Array.Empty<long>(), 0, capacity, 16, 16)));

    [Fact]
    public void ParticleData_UnknownDataTypeIsRejected()
        => Assert.Throws<InvalidDataException>(() => ParseData(ParticleData(80, Array.Empty<long>(), 2, 128, 16, 16)));

    [Fact]
    public void ParticleData_NoneSimulationSpaceIsRejected()
        => Assert.Throws<InvalidDataException>(() => ParseData(
            ParticleData(80, Array.Empty<long>(), 0, 128, 16, 16, int.MaxValue)));

    [Fact]
    public void SpawnerInitializeUpdateOutputChain_HasTypedProfilesAndTasks()
    {
        VfxContextSchema schema = VfxContextSchema.Create(Build(ParticleChain()));

        Assert.Equal(
            new[] { "Spawner", "Init", "Update", "Output" },
            schema.Contexts.Select(context => context.ContextType.ToString()));
        Assert.Equal(VfxDataType.SpawnEvent, schema.Contexts[0].OutputType);
        Assert.Equal(VfxDataType.Particle, schema.Contexts[1].OutputType);
        Assert.Equal(VfxTaskKind.Initialize, schema.Contexts[1].Task);
        Assert.Equal(VfxTaskKind.Update, schema.Contexts[2].Task);
        Assert.Equal(VfxTaskKind.ParticleQuadOutput, schema.Contexts[3].Task);
        Assert.Equal(3, Build(ParticleChain()).FlowEdges.Count);
    }

    [Theory]
    [InlineData(0, "ParticleTriangleOutput")]
    [InlineData(1, "ParticleQuadOutput")]
    [InlineData(2, "ParticleOctagonOutput")]
    public void PlanarOutput_MapsEveryOfficialPrimitiveTask(int primitiveType, string expected)
    {
        VfxContextDescriptor context = Assert.Single(ParseOutput(PlanarGuid, $"primitiveType: {primitiveType}\n  useGeometryShader: 0"));

        Assert.Equal(expected, context.Task.ToString());
    }

    [Fact]
    public void PlanarOutput_GeometryShaderUsesPointTaskLikeUnity14()
    {
        VfxContextDescriptor context = Assert.Single(ParseOutput(PlanarGuid, "primitiveType: 2\n  useGeometryShader: 1"));

        Assert.Equal(VfxTaskKind.ParticlePointOutput, context.Task);
    }

    [Fact]
    public void QuadStripOutput_RequiresStripDataAndUsesQuadTask()
    {
        VfxContextDescriptor context = Assert.Single(ParseOutput(QuadStripGuid, string.Empty, strip: true));

        Assert.Equal(VfxDataType.ParticleStrip, context.InputType);
        Assert.Equal(VfxTaskKind.ParticleQuadOutput, context.Task);
    }

    [Fact]
    public void CpuEvent_UsesUnityPlayEventDefaultAndPreservesCustomName()
    {
        VfxContextDescriptor defaultEvent = Assert.Single(ParseContextWithoutData(EventGuid, string.Empty, 1, 1));
        VfxContextDescriptor customEvent = Assert.Single(ParseContextWithoutData(EventGuid, "eventName: Fire", 1, 1));

        Assert.Equal("OnPlay", defaultEvent.EventName);
        Assert.Equal("Fire", customEvent.EventName);
        Assert.Equal(VfxContextType.Event, customEvent.ContextType);
    }

    [Fact]
    public void OutputEvent_HasSpawnToOutputEventProfileAndNoOutputFlowSlot()
    {
        VfxContextDescriptor context = Assert.Single(ParseContextWithoutData(OutputEventGuid, "eventName: Hit", 1, 0));

        Assert.Equal(VfxContextType.OutputEvent, context.ContextType);
        Assert.Equal(VfxDataType.SpawnEvent, context.InputType);
        Assert.Equal(VfxDataType.OutputEvent, context.OutputType);
        Assert.Equal("Hit", context.EventName);
    }

    [Fact]
    public void Context_InvalidFlowSlotCountIsRejected()
        => Assert.Throws<InvalidDataException>(() => ParseContextWithoutData(EventGuid, string.Empty, 0, 1));

    [Fact]
    public void Context_FlowBetweenParticleAndStripDataIsRejected()
    {
        string source = ParticleChain()
            .Replace("  m_Data: {fileID: 90}\n  m_InputFlowSlot:\n  - link:\n    - context: {fileID: 40}",
                "  m_Data: {fileID: 91}\n  m_InputFlowSlot:\n  - link:\n    - context: {fileID: 40}", StringComparison.Ordinal)
            .Replace("  - {fileID: 50}\n  dataType: 0", "  dataType: 0", StringComparison.Ordinal) +
            ParticleData(91, new long[] { 50 }, 1, 64, 1, 64);

        Assert.Throws<InvalidDataException>(() => VfxContextSchema.Create(Build(source)));
    }

    private static IReadOnlyList<VfxDataDescriptor> ParseData(string data)
    {
        string source = Preamble + Graph(Array.Empty<long>()) + data + Resource;
        return VfxContextSchema.Create(Build(source)).Data;
    }

    private static IReadOnlyList<VfxContextDescriptor> ParseOutput(string guid, string settings, bool strip = false)
    {
        string context = Context(20, guid, 80, 1, 1, Array.Empty<Flow>(), Array.Empty<Flow>(), settings);
        string data = ParticleData(80, new long[] { 20 }, strip ? 1 : 0, strip ? 64 : 128, strip ? 1 : 16, strip ? 64 : 16);
        string source = Preamble + Graph(new long[] { 20 }) + context + data + Resource;
        return VfxContextSchema.Create(Build(source)).Contexts;
    }

    private static IReadOnlyList<VfxContextDescriptor> ParseContextWithoutData(
        string guid,
        string settings,
        int inputSlots,
        int outputSlots)
    {
        string source = Preamble + Graph(new long[] { 20 }) +
                        Context(20, guid, 0, inputSlots, outputSlots, Array.Empty<Flow>(), Array.Empty<Flow>(), settings) +
                        Resource;
        return VfxContextSchema.Create(Build(source)).Contexts;
    }

    private static string ParticleChain()
        => Preamble + Graph(new long[] { 20, 30, 40, 50 }) +
           Context(20, SpawnerGuid, 80, 2, 1, Array.Empty<Flow>(), new[] { new Flow(30, 0) }) +
           Context(30, InitializeGuid, 90, 1, 1, new[] { new Flow(20, 0) }, new[] { new Flow(40, 0) }) +
           Context(40, UpdateGuid, 90, 1, 1, new[] { new Flow(30, 0) }, new[] { new Flow(50, 0) }) +
           Context(50, PlanarGuid, 90, 1, 1, new[] { new Flow(40, 0) }, Array.Empty<Flow>(),
               "primitiveType: 1\n  useGeometryShader: 0") +
           SpawnerData(80, new long[] { 20 }) +
           ParticleData(90, new long[] { 30, 40, 50 }, 0, 128, 16, 16) + Resource;

    private static VfxTypedGraph Build(string source) => VfxTypedGraph.Build(VfxYamlAsset.Parse(source));

    private static string Graph(IReadOnlyList<long> children)
        => "--- !u!114 &10\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {GraphGuid}, type: 3}}\n" +
           "  m_Name: Graph\n  m_Parent: {fileID: 0}\n" + References("m_Children", children) +
           "  m_InputSlots: []\n  m_OutputSlots: []\n";

    private static string Context(
        long id,
        string guid,
        long dataId,
        int inputSlotCount,
        int outputSlotCount,
        IReadOnlyList<Flow> inputs,
        IReadOnlyList<Flow> outputs,
        string settings = "")
        => $"--- !u!114 &{id}\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n" +
           $"  m_Name: Context{id}\n  m_Parent: {{fileID: 10}}\n  m_Children: []\n  m_Data: {{fileID: {dataId}}}\n" +
           FlowSlots("m_InputFlowSlot", inputSlotCount, inputs) +
           FlowSlots("m_OutputFlowSlot", outputSlotCount, outputs) +
           (settings.Length == 0 ? string.Empty : "  " + settings + "\n");

    private static string ParticleData(
        long id,
        IReadOnlyList<long> owners,
        int dataType,
        int capacity,
        int stripCapacity,
        int particlesPerStrip,
        int space = 0,
        int boundsMode = 0,
        int needsComputeBounds = 0)
        => $"--- !u!114 &{id}\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {ParticleDataGuid}, type: 3}}\n" +
           $"  m_Name: Data{id}\n  m_Parent: {{fileID: 0}}\n  m_Children: []\n" +
           References("m_Owners", owners) +
           $"  dataType: {dataType}\n  capacity: {capacity}\n  stripCapacity: {stripCapacity}\n" +
           $"  particlePerStripCount: {particlesPerStrip}\n  needsComputeBounds: {needsComputeBounds}\n" +
           $"  boundsMode: {boundsMode}\n  m_Space: {space}\n";

    private static string SpawnerData(long id, IReadOnlyList<long> owners)
        => $"--- !u!114 &{id}\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {SpawnerDataGuid}, type: 3}}\n" +
           $"  m_Name: Data{id}\n  m_Parent: {{fileID: 0}}\n  m_Children: []\n" + References("m_Owners", owners);

    private static string FlowSlots(string name, int count, IReadOnlyList<Flow> links)
    {
        if (count == 0) return $"  {name}: []\n";
        var source = new System.Text.StringBuilder().Append("  ").Append(name).Append(":\n");
        for (int slot = 0; slot < count; slot++)
        {
            Flow[] slotLinks = links.Where(link => link.LocalSlot == slot).ToArray();
            if (slotLinks.Length == 0) source.Append("  - link: []\n");
            else
            {
                source.Append("  - link:\n");
                foreach (Flow link in slotLinks)
                    source.Append("    - context: {fileID: ").Append(link.ContextId).Append("}\n")
                        .Append("      slotIndex: ").Append(link.RemoteSlot).Append('\n');
            }
        }
        return source.ToString();
    }

    private static string References(string name, IReadOnlyList<long> values)
        => values.Count == 0
            ? $"  {name}: []\n"
            : $"  {name}:\n" + string.Concat(values.Select(value => $"  - {{fileID: {value}}}\n"));

    private readonly struct Flow
    {
        internal Flow(long contextId, int remoteSlot, int localSlot = 0)
        {
            ContextId = contextId;
            RemoteSlot = remoteSlot;
            LocalSlot = localSlot;
        }
        internal long ContextId { get; }
        internal int RemoteSlot { get; }
        internal int LocalSlot { get; }
    }

    private const string Preamble = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";
    private const string Resource = "--- !u!2058629511 &900\nVisualEffectResource:\n  m_Graph: {fileID: 10}\n";
    private const string GraphGuid = "7d4c867f6b72b714dbb5fd1780afe208";
    private const string SpawnerGuid = "73a13919d81fb7444849bae8b5c812a2";
    private const string InitializeGuid = "9dfea48843f53fc438eabc12a3a30abc";
    private const string UpdateGuid = "2dc095764ededfa4bb32fa602511ea4b";
    private const string PlanarGuid = "a0b9e6b9139e58d4c957ec54595da7d3";
    private const string QuadStripGuid = "756b42789c29cb74085def1da319fa0b";
    private const string EventGuid = "2461f61b3c026d54db1951a4e17ab20e";
    private const string OutputEventGuid = "4f39de6f4fce95c4d9240e5055b057a6";
    private const string ParticleDataGuid = "d78581a96eae8bf4398c282eb0b098bd";
    private const string SpawnerDataGuid = "f68759077adc0b143b6e1c101e82065e";
}
