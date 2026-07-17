using System.Buffers.Binary;
using System.Collections.ObjectModel;
using UnityEditor.VFX.Model;

namespace UnityEditor.VFX.Generation;

/// <summary>
/// CPU event-record ABI emitted by VFXExpressionGraph.ComputeEventAttributeDescs.
/// spawnCount is always field zero and every field offset is measured in 32-bit words.
/// </summary>
internal sealed class VfxEventAttributeLayout
{
    private VfxEventAttributeLayout(IReadOnlyList<VfxEventAttributeField> fields, int structureSizeWords)
    {
        Fields = fields;
        StructureSizeWords = structureSizeWords;
    }

    internal IReadOnlyList<VfxEventAttributeField> Fields { get; }
    internal int StructureSizeWords { get; }
    internal int StructureSizeBytes => checked(StructureSizeWords * sizeof(uint));

    internal static VfxEventAttributeLayout Create(IEnumerable<VfxAttributeDefinition> attributes)
    {
        if (attributes is null) throw new ArgumentNullException(nameof(attributes));
        VfxAttributeDefinition spawnCount = VfxAttributeCatalog.Find("spawnCount");
        var definitions = new List<VfxAttributeDefinition> { spawnCount };
        var byName = new Dictionary<string, VfxAttributeDefinition>(StringComparer.Ordinal)
        {
            [spawnCount.Name] = spawnCount
        };
        foreach (VfxAttributeDefinition attribute in attributes)
        {
            if (attribute is null) throw new ArgumentException("VFX event attribute cannot be null.", nameof(attributes));
            if (byName.TryGetValue(attribute.Name, out VfxAttributeDefinition? existing))
            {
                if (existing.ValueType != attribute.ValueType)
                    throw new InvalidDataException(
                        $"VFX event attribute '{attribute.Name}' has conflicting value types.");
                continue;
            }
            byName.Add(attribute.Name, attribute);
            definitions.Add(attribute);
        }

        int structureSizeWords = definitions.Sum(definition => definition.ComponentCount);
        var fields = new List<VfxEventAttributeField>(definitions.Count);
        int offsetWords = 0;
        foreach (VfxAttributeDefinition definition in definitions)
        {
            fields.Add(new VfxEventAttributeField(definition, offsetWords, structureSizeWords));
            offsetWords = checked(offsetWords + definition.ComponentCount);
        }
        return new VfxEventAttributeLayout(
            new ReadOnlyCollection<VfxEventAttributeField>(fields),
            offsetWords);
    }

    internal byte[] PackRecords(
        IReadOnlyList<IReadOnlyDictionary<string, VfxEventAttributeValue>> records,
        int startEventIndex = 0)
    {
        if (records is null) throw new ArgumentNullException(nameof(records));
        if (startEventIndex < 0) throw new ArgumentOutOfRangeException(nameof(startEventIndex));
        int recordCount = checked(startEventIndex + records.Count);
        var bytes = new byte[checked(recordCount * StructureSizeBytes)];
        var fieldsByName = Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
        for (int recordIndex = 0; recordIndex < records.Count; recordIndex++)
        {
            IReadOnlyDictionary<string, VfxEventAttributeValue> record = records[recordIndex]
                ?? throw new ArgumentException("VFX event record cannot be null.", nameof(records));
            foreach (string name in record.Keys)
                if (!fieldsByName.ContainsKey(name))
                    throw new InvalidDataException($"VFX event record contains unknown attribute '{name}'.");
            foreach ((string name, VfxEventAttributeValue value) in record)
            {
                VfxEventAttributeField field = fieldsByName[name];
                if (field.ValueType != value.ValueType || field.SizeWords != value.Words.Count)
                    throw new InvalidDataException(
                        $"VFX event attribute '{name}' value does not match layout type '{field.ValueType}'.");
                int destination = checked(
                    ((startEventIndex + recordIndex) * StructureSizeWords + field.ElementOffsetWords) * sizeof(uint));
                for (int component = 0; component < value.Words.Count; component++)
                    BinaryPrimitives.WriteUInt32LittleEndian(
                        bytes.AsSpan(destination + component * sizeof(uint), sizeof(uint)),
                        value.Words[component]);
            }
        }
        return bytes;
    }
}

internal sealed class VfxEventAttributeField
{
    internal VfxEventAttributeField(
        VfxAttributeDefinition definition,
        int elementOffsetWords,
        int structureSizeWords)
    {
        Definition = definition;
        ElementOffsetWords = elementOffsetWords;
        StructureSizeWords = structureSizeWords;
    }

    internal VfxAttributeDefinition Definition { get; }
    internal string Name => Definition.Name;
    internal VfxAttributeValueType ValueType => Definition.ValueType;
    internal int ElementOffsetWords { get; }
    internal int StructureSizeWords { get; }
    internal int SizeWords => Definition.ComponentCount;
}

internal sealed class VfxEventAttributeValue
{
    private VfxEventAttributeValue(VfxAttributeValueType valueType, params uint[] words)
    {
        ValueType = valueType;
        Words = new ReadOnlyCollection<uint>(words);
    }

    internal VfxAttributeValueType ValueType { get; }
    internal IReadOnlyList<uint> Words { get; }

    internal static VfxEventAttributeValue Boolean(bool value)
        => new(VfxAttributeValueType.Boolean, value ? 1u : 0u);

    internal static VfxEventAttributeValue UInt32(uint value)
        => new(VfxAttributeValueType.UInt32, value);

    internal static VfxEventAttributeValue Int32(int value)
        => new(VfxAttributeValueType.Int32, unchecked((uint)value));

    internal static VfxEventAttributeValue Float(float value)
        => new(VfxAttributeValueType.Float, FloatWord(value));

    internal static VfxEventAttributeValue Float2(float x, float y)
        => new(VfxAttributeValueType.Float2, FloatWord(x), FloatWord(y));

    internal static VfxEventAttributeValue Float3(float x, float y, float z)
        => new(VfxAttributeValueType.Float3, FloatWord(x), FloatWord(y), FloatWord(z));

    internal static VfxEventAttributeValue Float4(float x, float y, float z, float w)
        => new(VfxAttributeValueType.Float4, FloatWord(x), FloatWord(y), FloatWord(z), FloatWord(w));

    private static uint FloatWord(float value)
        => unchecked((uint)BitConverter.SingleToInt32Bits(value));
}
