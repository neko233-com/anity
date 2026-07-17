using UnityEditor.VFX.Generation;
using UnityEditor.VFX.Model;
using UnityEditor.VFX.Serialization;
using UnityEngine.VFX;
using Xunit;

namespace Unity.VisualEffectGraph.Editor.Tests;

public sealed class VfxRuntimeBoundsCompilerTests
{
    [Fact]
    public void RecordedBounds_CompileCenterSizeAndPadding()
    {
        VFXRuntimeSystemData system = Compile(boundsMode: 0, space: 0, padding: (1f, 2f, 3f));

        Assert.True(system.HasStaticBounds);
        Assert.False(system.BoundsInWorldSpace);
        Assert.Equal((2f, 3f, 4f), (system.BoundsCenterX, system.BoundsCenterY, system.BoundsCenterZ));
        Assert.Equal((12f, 24f, 36f), (system.BoundsSizeX, system.BoundsSizeY, system.BoundsSizeZ));
    }

    [Fact]
    public void ManualBounds_IgnoreRecordedPadding()
    {
        VFXRuntimeSystemData system = Compile(boundsMode: 1, space: 0, padding: (9f, 9f, 9f));

        Assert.True(system.HasStaticBounds);
        Assert.Equal((10f, 20f, 30f), (system.BoundsSizeX, system.BoundsSizeY, system.BoundsSizeZ));
    }

    [Fact]
    public void WorldSimulationSpace_IsPreservedInRuntimeContract()
    {
        VFXRuntimeSystemData system = Compile(boundsMode: 1, space: 1);

        Assert.True(system.HasStaticBounds);
        Assert.True(system.BoundsInWorldSpace);
    }

    [Fact]
    public void AutomaticBounds_CompileRuntimeReductionLayout()
    {
        VFXRuntimeSystemData system = CompileAutomatic(space: 0, padding: (1f, 2f, 3f));

        Assert.False(system.HasStaticBounds);
        Assert.True(system.HasAutomaticBounds);
        Assert.False(system.BoundsInWorldSpace);
        Assert.True(system.PositionOffsetWords >= 0);
        Assert.True(system.AliveOffsetWords >= 0);
        Assert.True(system.SizeOffsetWords >= 0);
        Assert.True(system.ScaleXOffsetWords >= 0);
        Assert.True(system.ScaleYOffsetWords >= 0);
        Assert.True(system.ScaleZOffsetWords >= 0);
        Assert.Equal((1f, 2f, 3f),
            (system.AutomaticBoundsPaddingX,
             system.AutomaticBoundsPaddingY,
             system.AutomaticBoundsPaddingZ));
    }

    [Fact]
    public void AutomaticBounds_WorldSimulationSpaceIsPreserved()
    {
        VFXRuntimeSystemData system = CompileAutomatic(space: 1, padding: (0f, 0f, 0f));

        Assert.True(system.HasAutomaticBounds);
        Assert.True(system.BoundsInWorldSpace);
    }

    [Fact]
    public void RuntimeV13_RoundTripsAutomaticBoundsMetadata()
    {
        VfxTypedGraph graph = Build(AutomaticAsset(space: 0, padding: (0.5f, 1f, 1.5f)));
        VFXRuntimeSystemData system = Assert.Single(
            VFXRuntimeAssetData.Deserialize(VfxRuntimeAssetCompiler.Compile(graph)).Systems);

        Assert.True(system.HasAutomaticBounds);
        Assert.Equal((0.5f, 1f, 1.5f),
            (system.AutomaticBoundsPaddingX,
             system.AutomaticBoundsPaddingY,
             system.AutomaticBoundsPaddingZ));
        Assert.True(system.PositionOffsetWords < system.SizeOffsetWords);
    }

    [Fact]
    public void AutomaticBounds_UnresolvedLinkedPaddingIsRejectedByTypedGraphGate()
    {
        string source = AutomaticAsset(space: 0, padding: (1f, 2f, 3f))
            .Replace("  m_LinkedSlots: []\n--- !u!2058629511",
                "  m_LinkedSlots:\n  - {fileID: 999}\n--- !u!2058629511",
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() =>
            VfxRuntimeAssetCompiler.Compile(Build(source)));
    }

    [Fact]
    public void RuntimeV12_RoundTripsCompiledStaticBounds()
    {
        VfxTypedGraph graph = Build(Asset(boundsMode: 0, space: 1, needsComputeBounds: 0, padding: (0.5f, 1f, 1.5f)));
        byte[] bytes = VfxRuntimeAssetCompiler.Compile(graph);
        VFXRuntimeSystemData system = Assert.Single(VFXRuntimeAssetData.Deserialize(bytes).Systems);

        Assert.True(system.HasStaticBounds);
        Assert.True(system.BoundsInWorldSpace);
        Assert.Equal((11f, 22f, 33f), (system.BoundsSizeX, system.BoundsSizeY, system.BoundsSizeZ));
    }

    [Fact]
    public void NonFiniteOrNegativeStaticBounds_AreNotPublished()
    {
        VFXRuntimeSystemData system = Compile(boundsMode: 1, space: 0, size: (-1f, 20f, 30f));

        Assert.False(system.HasStaticBounds);
    }

    private static VFXRuntimeSystemData Compile(
        int boundsMode,
        int space,
        int needsComputeBounds = 0,
        (float X, float Y, float Z)? padding = null,
        (float X, float Y, float Z)? size = null)
    {
        VfxTypedGraph graph = Build(Asset(
            boundsMode, space, needsComputeBounds,
            padding ?? (0f, 0f, 0f), size ?? (10f, 20f, 30f)));
        return Assert.Single(VFXRuntimeAssetData.Deserialize(VfxRuntimeAssetCompiler.Compile(graph)).Systems);
    }

    private static VFXRuntimeSystemData CompileAutomatic(
        int space,
        (float X, float Y, float Z) padding)
        => Assert.Single(VFXRuntimeAssetData.Deserialize(
            VfxRuntimeAssetCompiler.Compile(Build(AutomaticAsset(space, padding)))).Systems);

    private static string AutomaticAsset(
        int space,
        (float X, float Y, float Z) padding)
    {
        string source = VfxContextKernelCompilerTests.InitializeUpdatePlanarOutputGraph()
            .Replace(
                "needsComputeBounds: 0\n  boundsMode: 0\n  m_Space: 0\n",
                $"needsComputeBounds: 1\n  boundsMode: 2\n  m_Space: {space}\n",
                StringComparison.Ordinal);
        int initialize = source.IndexOf("--- !u!114 &50\n", StringComparison.Ordinal);
        int inputSlots = source.IndexOf("  m_InputSlots: []\n", initialize, StringComparison.Ordinal);
        if (initialize < 0 || inputSlots < 0)
            throw new InvalidOperationException("Synthetic Initialize context was not found.");
        source = source.Remove(inputSlots, "  m_InputSlots: []\n".Length)
            .Insert(inputSlots, "  m_InputSlots:\n  - {fileID: 400}\n");
        string paddingSlot = SlotDocument(
            400, Float3SlotGuid, "boundsPadding", "UnityEngine.Vector3, UnityEngine.CoreModule",
            $"{{\"x\":{padding.X},\"y\":{padding.Y},\"z\":{padding.Z}}}",
            ownerId: 50);
        return source.Replace(
            "--- !u!2058629511 &900\n",
            paddingSlot + "--- !u!2058629511 &900\n",
            StringComparison.Ordinal);
    }

    private static VfxTypedGraph Build(string source)
        => VfxTypedGraph.Build(VfxYamlAsset.Parse(source));

    private static string Asset(
        int boundsMode,
        int space,
        int needsComputeBounds,
        (float X, float Y, float Z) padding,
        (float X, float Y, float Z)? size = null)
    {
        (float X, float Y, float Z) resolvedSize = size ?? (10f, 20f, 30f);
        return Preamble +
               GraphDocument() +
               InitializeDocument() +
               ParticleData(boundsMode, space, needsComputeBounds) +
               BoundsSlot(resolvedSize) +
               PaddingSlot(padding) +
               "--- !u!2058629511 &90\nVisualEffectResource:\n  m_Graph: {fileID: 10}\n";
    }

    private static string GraphDocument()
        => "--- !u!114 &10\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {GraphGuid}, type: 3}}\n" +
           "  m_Name: Graph\n  m_Parent: {fileID: 0}\n  m_Children:\n  - {fileID: 20}\n" +
           "  m_InputSlots: []\n  m_OutputSlots: []\n";

    private static string InitializeDocument()
        => "--- !u!114 &20\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {InitializeGuid}, type: 3}}\n" +
           "  m_Name: Initialize\n  m_Parent: {fileID: 10}\n  m_Children: []\n" +
           "  m_InputSlots:\n  - {fileID: 30}\n  - {fileID: 40}\n  m_OutputSlots: []\n" +
           "  m_Data: {fileID: 80}\n" +
           "  m_InputFlowSlot:\n  - link: []\n  m_OutputFlowSlot:\n  - link: []\n";

    private static string ParticleData(int boundsMode, int space, int needsComputeBounds)
        => "--- !u!114 &80\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {ParticleDataGuid}, type: 3}}\n" +
           "  m_Name: ParticleData\n  m_Parent: {fileID: 0}\n  m_Children: []\n" +
           "  m_Owners:\n  - {fileID: 20}\n  title: Particles\n  dataType: 0\n  capacity: 64\n" +
           "  stripCapacity: 1\n  particlePerStripCount: 1\n" +
           $"  needsComputeBounds: {needsComputeBounds}\n  boundsMode: {boundsMode}\n  m_Space: {space}\n";

    private static string BoundsSlot((float X, float Y, float Z) size)
        => SlotDocument(
            30, GenericSlotGuid, "bounds", "UnityEngine.VFX.AABox, UnityEngine.VFXModule",
            $"{{\"center\":{{\"x\":2,\"y\":3,\"z\":4}},\"size\":{{\"x\":{size.X},\"y\":{size.Y},\"z\":{size.Z}}}}}");

    private static string PaddingSlot((float X, float Y, float Z) padding)
        => SlotDocument(
            40, Float3SlotGuid, "boundsPadding", "UnityEngine.Vector3, UnityEngine.CoreModule",
            $"{{\"x\":{padding.X},\"y\":{padding.Y},\"z\":{padding.Z}}}");

    private static string SlotDocument(
        long id, string guid, string name, string type, string serializedObject,
        long ownerId = 20)
        => $"--- !u!114 &{id}\nMonoBehaviour:\n" +
           $"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n" +
           $"  m_Name: {name}\n  m_Parent: {{fileID: 0}}\n  m_Children: []\n" +
           $"  m_MasterSlot: {{fileID: {id}}}\n  m_MasterData:\n    m_Owner: {{fileID: {ownerId}}}\n" +
           $"    m_Value:\n      m_Type:\n        m_SerializableType: {type}\n" +
           $"      m_SerializableObject: {serializedObject}\n    m_Space: 0\n" +
           $"  m_Property:\n    name: {name}\n    m_serializedType:\n      m_SerializableType: {type}\n" +
           "  m_Direction: 0\n  m_LinkedSlots: []\n";

    private const string Preamble = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";
    private const string GraphGuid = "7d4c867f6b72b714dbb5fd1780afe208";
    private const string InitializeGuid = "9dfea48843f53fc438eabc12a3a30abc";
    private const string ParticleDataGuid = "d78581a96eae8bf4398c282eb0b098bd";
    private const string GenericSlotGuid = "1b605c022ee79394a8a776c0869b3f9a";
    private const string Float3SlotGuid = "ac39bd03fca81b849929b9c966f1836a";
}
