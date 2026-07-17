using System.Buffers.Binary;
using System.Security.Cryptography;
using UnityEditor.VFX.Generation;
using UnityEditor.VFX.Model;
using UnityEditor.VFX.Serialization;
using UnityEngine.VFX;
using Xunit;

namespace Unity.VisualEffectGraph.Editor.Tests;

public sealed class VfxPlanarRuntimeDescriptorTests
{
    [Fact]
    public void RuntimeV15_WritesPlanarOutputContractVersion()
    {
        byte[] bytes = CompileBytes();

        Assert.Equal(15, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4, 4)));
    }

    [Theory]
    [InlineData(0, 3, new[] { 0, 1, 2 })]
    [InlineData(1, 4, new[] { 0, 2, 1, 1, 2, 3 })]
    [InlineData(2, 8, new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 6, 0, 6, 7 })]
    public void PrimitiveGeometry_RoundTripsExactUnityTopology(
        int primitiveType,
        int expectedVertices,
        int[] expectedIndices)
    {
        VFXRuntimePlanarOutputData output = CompileOutput(primitiveType: primitiveType);

        Assert.Equal(primitiveType, output.PrimitiveType);
        Assert.Equal(expectedVertices, output.VerticesPerParticle);
        Assert.Equal(expectedIndices, output.IndexPattern);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void UvMode_RoundTripsEveryOfficialLegacyMode(int uvMode)
        => Assert.Equal(uvMode, CompileOutput(uvMode: uvMode).UvMode);

    [Fact]
    public void DefaultAlphaOutput_RoundTripsRenderState()
    {
        VFXRuntimePlanarOutputData output = CompileOutput();

        Assert.Equal(1, output.BlendMode);
        Assert.Equal(0, output.CullMode);
        Assert.False(output.ZWrite);
        Assert.Equal(2, output.ZTest);
        Assert.False(output.AlphaClipping);
        Assert.Equal("Transparent", output.RenderQueue);
        Assert.True(output.RequiresSorting);
        Assert.False(output.IndirectDraw);
    }

    [Fact]
    public void OpaqueClippedOutput_RoundTripsQueueAndDrawFlags()
    {
        VFXRuntimePlanarOutputData output = CompileOutput(outputSettings:
            "  blendMode: 3\n" +
            "  cullMode: 2\n" +
            "  zWriteMode: 2\n" +
            "  zTestMode: 7\n" +
            "  useAlphaClipping: 1\n" +
            "  sortingPriority: 7\n" +
            "  sort: 2\n" +
            "  indirectDraw: 1\n");

        Assert.Equal(3, output.BlendMode);
        Assert.Equal(2, output.CullMode);
        Assert.True(output.ZWrite);
        Assert.Equal(6, output.ZTest);
        Assert.True(output.AlphaClipping);
        Assert.Equal("AlphaTest+7", output.RenderQueue);
        Assert.True(output.RequiresSorting);
        Assert.True(output.IndirectDraw);
    }

    [Fact]
    public void ExecutableLegacyOutput_IsMarkedRenderable()
        => Assert.True(CompileOutput().RuntimeExecutable);

    [Fact]
    public void UnsupportedShaderGraphOutput_PreservesDescriptorWithoutClaimingExecution()
    {
        VFXRuntimePlanarOutputData output = CompileOutput(shaderGraphFileId: 999);

        Assert.False(output.RuntimeExecutable);
        Assert.Equal(4, output.VerticesPerParticle);
        Assert.Equal("System", output.ParticleSystemName);
        Assert.NotEmpty(output.Attributes);
    }

    [Fact]
    public void ParticleLayout_IsPackedAndContainsEveryRequiredPlanarAttribute()
    {
        VFXRuntimePlanarOutputData output = CompileOutput();
        string[] required =
        {
            "position", "color", "alpha", "alive",
            "axisX", "axisY", "axisZ",
            "angleX", "angleY", "angleZ",
            "pivotX", "pivotY", "pivotZ",
            "size", "scaleX", "scaleY", "scaleZ"
        };

        Assert.Equal(27, output.AttributeStrideWords);
        Assert.Equal(0, output.Attributes[0].OffsetWords);
        Assert.Equal(
            output.AttributeStrideWords,
            output.Attributes.Sum(attribute => attribute.SizeWords));
        foreach (string name in required)
            Assert.Contains(output.Attributes, attribute => attribute.Name == name);
    }

    [Fact]
    public void RuntimeAssetCompiler_ImportsPlanarDescriptorIntoVisualEffectAsset()
    {
        VfxTypedGraph graph = Build();
        var asset = new VisualEffectAsset();

        VfxRuntimeAssetCompiler.CompileInto(graph, asset);

        VFXRuntimePlanarOutputData output = Assert.Single(asset.GetPlanarOutputs());
        Assert.Equal(70, output.ContextId);
        Assert.True(output.RuntimeExecutable);
    }

    [Fact]
    public void RuntimeAssetCompiler_IsByteDeterministicWithPlanarDescriptor()
    {
        VfxTypedGraph graph = Build(outputSettings: "  blendMode: 0\n  indirectDraw: 1\n");

        Assert.Equal(VfxRuntimeAssetCompiler.Compile(graph), VfxRuntimeAssetCompiler.Compile(graph));
    }

    [Fact]
    public void RuntimeV15_RejectsDuplicatePlanarContextIds()
    {
        VFXRuntimeAssetData data = CompileData();
        VFXRuntimePlanarOutputData output = Assert.Single(data.PlanarOutputs);

        Assert.Throws<InvalidDataException>(() =>
            (data with { PlanarOutputs = new[] { output, output } }).Serialize());
    }

    [Fact]
    public void RuntimeV15_RejectsTopologyThatDoesNotMatchPrimitive()
    {
        VFXRuntimeAssetData data = CompileData();
        VFXRuntimePlanarOutputData output = Assert.Single(data.PlanarOutputs);

        Assert.Throws<InvalidDataException>(() =>
            (data with
            {
                PlanarOutputs = new[] { output with { IndexPattern = new[] { 0, 1, 3 } } }
            }).Serialize());
    }

    [Fact]
    public void RuntimeV15_RejectsWrongRequiredAttributeType()
    {
        VFXRuntimeAssetData data = CompileData();
        VFXRuntimePlanarOutputData output = Assert.Single(data.PlanarOutputs);
        VFXRuntimeAttributeData[] attributes = output.Attributes
            .Select(attribute => attribute.Name == "alive"
                ? attribute with { ValueType = VFXRuntimeValueType.Float }
                : attribute)
            .ToArray();

        Assert.Throws<InvalidDataException>(() =>
            (data with
            {
                PlanarOutputs = new[] { output with { Attributes = attributes } }
            }).Serialize());
    }

    [Fact]
    public void RuntimeV15_RejectsParticleStrideMismatch()
    {
        VFXRuntimeAssetData data = CompileData();
        VFXRuntimePlanarOutputData output = Assert.Single(data.PlanarOutputs);

        Assert.Throws<InvalidDataException>(() =>
            (data with
            {
                PlanarOutputs = new[]
                {
                    output with { AttributeStrideWords = output.AttributeStrideWords + 1 }
                }
            }).Serialize());
    }

    [Fact]
    public void RuntimeV14_BackwardCompatibilityImportsNoPlanarDescriptors()
    {
        VFXRuntimeAssetData data = CompileData() with
        {
            PlanarOutputs = Array.Empty<VFXRuntimePlanarOutputData>()
        };
        byte[] v14 = ConvertEmptyPlanarV15ToV14(data.Serialize());

        VFXRuntimeAssetData restored = VFXRuntimeAssetData.Deserialize(v14);

        Assert.Empty(restored.PlanarOutputs);
        Assert.NotEmpty(restored.UpdateKernels);
    }

    private static VFXRuntimePlanarOutputData CompileOutput(
        int uvMode = 0,
        long shaderGraphFileId = 0,
        int primitiveType = 1,
        string outputSettings = "")
        => Assert.Single(CompileData(
            uvMode, shaderGraphFileId, primitiveType, outputSettings).PlanarOutputs);

    private static VFXRuntimeAssetData CompileData(
        int uvMode = 0,
        long shaderGraphFileId = 0,
        int primitiveType = 1,
        string outputSettings = "")
        => VFXRuntimeAssetData.Deserialize(CompileBytes(
            uvMode, shaderGraphFileId, primitiveType, outputSettings));

    private static byte[] CompileBytes(
        int uvMode = 0,
        long shaderGraphFileId = 0,
        int primitiveType = 1,
        string outputSettings = "")
        => VfxRuntimeAssetCompiler.Compile(Build(
            uvMode, shaderGraphFileId, primitiveType, outputSettings));

    private static VfxTypedGraph Build(
        int uvMode = 0,
        long shaderGraphFileId = 0,
        int primitiveType = 1,
        string outputSettings = "")
        => VfxTypedGraph.Build(VfxYamlAsset.Parse(
            VfxContextKernelCompilerTests.InitializeUpdatePlanarOutputGraph(
                uvMode,
                shaderGraphFileId,
                primitiveType,
                outputSettings: outputSettings)));

    private static byte[] ConvertEmptyPlanarV15ToV14(byte[] v15)
    {
        int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(v15.AsSpan(8, 4));
        int v14PayloadLength = checked(payloadLength - sizeof(int));
        byte[] converted = new byte[checked(12 + v14PayloadLength + 32)];
        v15.AsSpan(0, 4).CopyTo(converted);
        BinaryPrimitives.WriteInt32LittleEndian(converted.AsSpan(4, 4), 14);
        BinaryPrimitives.WriteInt32LittleEndian(converted.AsSpan(8, 4), v14PayloadLength);
        v15.AsSpan(12, v14PayloadLength).CopyTo(converted.AsSpan(12));
        byte[] hash = SHA256.HashData(converted.AsSpan(12, v14PayloadLength));
        hash.CopyTo(converted.AsSpan(12 + v14PayloadLength));
        return converted;
    }
}
