using System.Buffers.Binary;
using UnityEditor.VFX.Generation;
using UnityEditor.VFX.Model;
using Xunit;

namespace Unity.VisualEffectGraph.Editor.Tests;

public sealed class VfxEventAttributeLayoutTests
{
    [Fact]
    public void Layout_AlwaysPlacesSpawnCountFirst()
    {
        VfxEventAttributeLayout layout = VfxEventAttributeLayout.Create(
            new[] { VfxAttributeCatalog.Find("position") });

        Assert.Equal(new[] { "spawnCount", "position" }, layout.Fields.Select(field => field.Name));
        Assert.Equal(0, layout.Fields[0].ElementOffsetWords);
    }

    [Fact]
    public void Layout_DeduplicatesExplicitSpawnCount()
    {
        VfxEventAttributeLayout layout = VfxEventAttributeLayout.Create(
            new[] { VfxAttributeCatalog.Find("spawnCount"), VfxAttributeCatalog.Find("size") });

        Assert.Equal(new[] { "spawnCount", "size" }, layout.Fields.Select(field => field.Name));
    }

    [Fact]
    public void Layout_PreservesFirstSeenAttributeOrder()
    {
        VfxEventAttributeLayout layout = VfxEventAttributeLayout.Create(new[]
        {
            VfxAttributeCatalog.Find("size"),
            VfxAttributeCatalog.Find("position"),
            VfxAttributeCatalog.Find("color")
        });

        Assert.Equal(new[] { "spawnCount", "size", "position", "color" },
            layout.Fields.Select(field => field.Name));
    }

    [Fact]
    public void Layout_EmitsUnityElementAndStructureWordOffsets()
    {
        VfxEventAttributeLayout layout = VfxEventAttributeLayout.Create(new[]
        {
            VfxAttributeCatalog.Find("position"),
            VfxAttributeCatalog.Find("size")
        });

        Assert.Equal(5, layout.StructureSizeWords);
        Assert.Equal(20, layout.StructureSizeBytes);
        Assert.Equal(new[] { 0, 1, 4 }, layout.Fields.Select(field => field.ElementOffsetWords));
        Assert.All(layout.Fields, field => Assert.Equal(5, field.StructureSizeWords));
    }

    [Fact]
    public void Layout_DeduplicatesMatchingDefinitionsByName()
    {
        VfxAttributeDefinition size = VfxAttributeCatalog.Find("size");
        VfxEventAttributeLayout layout = VfxEventAttributeLayout.Create(new[] { size, size });

        Assert.Equal(2, layout.Fields.Count);
    }

    [Fact]
    public void Layout_RejectsSameNameWithConflictingType()
    {
        VfxAttributeDefinition scalar = VfxAttributeCatalog.CreateCustom("payload", 0);
        VfxAttributeDefinition vector = VfxAttributeCatalog.CreateCustom("payload", 2);

        Assert.Throws<InvalidDataException>(() => VfxEventAttributeLayout.Create(new[] { scalar, vector }));
    }

    [Fact]
    public void PackRecords_WritesScalarAndVectorWordsAtDeclaredOffsets()
    {
        VfxEventAttributeLayout layout = VfxEventAttributeLayout.Create(new[]
        {
            VfxAttributeCatalog.Find("position"),
            VfxAttributeCatalog.Find("size")
        });
        var record = new Dictionary<string, VfxEventAttributeValue>
        {
            ["spawnCount"] = VfxEventAttributeValue.Float(2.0f),
            ["position"] = VfxEventAttributeValue.Float3(3.0f, 4.0f, 5.0f),
            ["size"] = VfxEventAttributeValue.Float(6.0f)
        };

        byte[] bytes = layout.PackRecords(new[] { record });

        Assert.Equal(new[] { 2.0f, 3.0f, 4.0f, 5.0f, 6.0f }, ReadFloats(bytes));
    }

    [Fact]
    public void PackRecords_WritesBooleanIntAndUintBitPatterns()
    {
        VfxEventAttributeLayout layout = VfxEventAttributeLayout.Create(new[]
        {
            VfxAttributeCatalog.Find("alive"),
            VfxAttributeCatalog.Find("meshIndex"),
            VfxAttributeCatalog.CreateCustom("signedPayload", 6)
        });
        var record = new Dictionary<string, VfxEventAttributeValue>
        {
            ["alive"] = VfxEventAttributeValue.Boolean(true),
            ["meshIndex"] = VfxEventAttributeValue.UInt32(0xf0000001u),
            ["signedPayload"] = VfxEventAttributeValue.Int32(-3)
        };

        byte[] bytes = layout.PackRecords(new[] { record });

        Assert.Equal(1u, ReadWord(bytes, 1));
        Assert.Equal(0xf0000001u, ReadWord(bytes, 2));
        Assert.Equal(unchecked((uint)-3), ReadWord(bytes, 3));
    }

    [Fact]
    public void PackRecords_LeavesMissingAttributesZeroInitialized()
    {
        VfxEventAttributeLayout layout = VfxEventAttributeLayout.Create(
            new[] { VfxAttributeCatalog.Find("position") });

        byte[] bytes = layout.PackRecords(new[]
        {
            (IReadOnlyDictionary<string, VfxEventAttributeValue>)new Dictionary<string, VfxEventAttributeValue>()
        });

        Assert.All(bytes, value => Assert.Equal(0, value));
    }

    [Fact]
    public void PackRecords_PacksMultipleRecordsWithStructureStride()
    {
        VfxEventAttributeLayout layout = VfxEventAttributeLayout.Create(
            new[] { VfxAttributeCatalog.Find("size") });
        IReadOnlyDictionary<string, VfxEventAttributeValue>[] records =
        {
            new Dictionary<string, VfxEventAttributeValue> { ["size"] = VfxEventAttributeValue.Float(7.0f) },
            new Dictionary<string, VfxEventAttributeValue> { ["size"] = VfxEventAttributeValue.Float(9.0f) }
        };

        byte[] bytes = layout.PackRecords(records);

        Assert.Equal(7.0f, ReadFloat(bytes, 1));
        Assert.Equal(9.0f, ReadFloat(bytes, 3));
    }

    [Fact]
    public void PackRecords_StartEventIndexLeavesPrefixRecordsUntouched()
    {
        VfxEventAttributeLayout layout = VfxEventAttributeLayout.Create(
            new[] { VfxAttributeCatalog.Find("size") });
        var record = new Dictionary<string, VfxEventAttributeValue>
        {
            ["size"] = VfxEventAttributeValue.Float(11.0f)
        };

        byte[] bytes = layout.PackRecords(new[] { record }, startEventIndex: 2);

        Assert.Equal(3 * layout.StructureSizeBytes, bytes.Length);
        Assert.All(bytes.Take(2 * layout.StructureSizeBytes), value => Assert.Equal(0, value));
        Assert.Equal(11.0f, ReadFloat(bytes, 5));
    }

    [Fact]
    public void PackRecords_RejectsUnknownAttribute()
    {
        VfxEventAttributeLayout layout = VfxEventAttributeLayout.Create(Array.Empty<VfxAttributeDefinition>());
        var record = new Dictionary<string, VfxEventAttributeValue>
        {
            ["unknown"] = VfxEventAttributeValue.Float(1.0f)
        };

        Assert.Throws<InvalidDataException>(() => layout.PackRecords(new[] { record }));
    }

    [Fact]
    public void PackRecords_RejectsValueTypeMismatch()
    {
        VfxEventAttributeLayout layout = VfxEventAttributeLayout.Create(
            new[] { VfxAttributeCatalog.Find("position") });
        var record = new Dictionary<string, VfxEventAttributeValue>
        {
            ["position"] = VfxEventAttributeValue.Float(1.0f)
        };

        Assert.Throws<InvalidDataException>(() => layout.PackRecords(new[] { record }));
    }

    [Fact]
    public void PackRecords_RejectsNegativeStartEventIndex()
    {
        VfxEventAttributeLayout layout = VfxEventAttributeLayout.Create(Array.Empty<VfxAttributeDefinition>());

        Assert.Throws<ArgumentOutOfRangeException>(() => layout.PackRecords(
            Array.Empty<IReadOnlyDictionary<string, VfxEventAttributeValue>>(), -1));
    }

    private static uint ReadWord(byte[] bytes, int wordIndex)
        => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(wordIndex * sizeof(uint), sizeof(uint)));

    private static float ReadFloat(byte[] bytes, int wordIndex)
        => BitConverter.Int32BitsToSingle(unchecked((int)ReadWord(bytes, wordIndex)));

    private static float[] ReadFloats(byte[] bytes)
        => Enumerable.Range(0, bytes.Length / sizeof(uint)).Select(index => ReadFloat(bytes, index)).ToArray();
}
