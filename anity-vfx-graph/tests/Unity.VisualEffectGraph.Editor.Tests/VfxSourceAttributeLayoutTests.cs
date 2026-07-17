using UnityEditor.VFX.Generation;
using UnityEditor.VFX.Model;
using Xunit;

namespace Unity.VisualEffectGraph.Editor.Tests;

public sealed class VfxSourceAttributeLayoutTests
{
    [Theory]
    [InlineData("size", "size:0:4", 4)]
    [InlineData("position", "position:0:12", 16)]
    [InlineData("position,size", "position:0:12,size:12:4", 16)]
    [InlineData("position,velocity", "position:0:12,velocity:16:12", 32)]
    [InlineData("position,velocity,size", "position:0:12,velocity:16:12,size:12:4", 32)]
    [InlineData("position,color,size,alpha", "position:0:12,color:16:12,size:12:4,alpha:28:4", 32)]
    [InlineData("size,alpha,lifetime,age", "size:0:4,alpha:4:4,lifetime:8:4,age:12:4", 16)]
    [InlineData("custom2,size", "custom2:0:8,size:8:4", 16)]
    [InlineData("custom2a,custom2b", "custom2a:0:8,custom2b:8:8", 16)]
    [InlineData("custom4,size", "custom4:0:16,size:16:4", 32)]
    [InlineData("position,size,alpha", "position:0:12,size:12:4,alpha:16:4", 32)]
    public void SourceLayout_MatchesUnityStructureOfArrayBucketPacking(
        string attributeNames,
        string expectedFields,
        int expectedStrideBytes)
    {
        VfxAttributeDefinition[] attributes = attributeNames.Split(',').Select(Definition).ToArray();

        (IReadOnlyList<VfxAttributeLayoutField> fields, int stride) =
            VfxContextKernelCompilation.CreateSourceLayout(attributes);

        Assert.Equal(expectedStrideBytes, stride);
        Assert.Equal(expectedFields, string.Join(",", fields.Select(field =>
            $"{field.Name}:{field.OffsetBytes}:{field.SizeBytes}")));
    }

    [Fact]
    public void SourceLayout_PreservesDeclarationOrderWhileOffsetsFollowPackedBuckets()
    {
        VfxAttributeDefinition[] attributes =
        {
            VfxAttributeCatalog.Find("size"),
            VfxAttributeCatalog.Find("position"),
            VfxAttributeCatalog.Find("alpha")
        };

        (IReadOnlyList<VfxAttributeLayoutField> fields, int stride) =
            VfxContextKernelCompilation.CreateSourceLayout(attributes);

        Assert.Equal(new[] { "size", "position", "alpha" }, fields.Select(field => field.Name));
        Assert.Equal(new[] { 12, 0, 16 }, fields.Select(field => field.OffsetBytes));
        Assert.Equal(32, stride);
    }

    [Fact]
    public void SourceLayout_EmptyContractHasZeroStrideAndNoFields()
    {
        (IReadOnlyList<VfxAttributeLayoutField> fields, int stride) =
            VfxContextKernelCompilation.CreateSourceLayout(Array.Empty<VfxAttributeDefinition>());

        Assert.Empty(fields);
        Assert.Equal(0, stride);
    }

    private static VfxAttributeDefinition Definition(string name)
        => name switch
        {
            "custom2" or "custom2a" or "custom2b" => VfxAttributeCatalog.CreateCustom(name, 1),
            "custom4" => VfxAttributeCatalog.CreateCustom(name, 3),
            _ => VfxAttributeCatalog.Find(name)
        };
}
