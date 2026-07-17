using System.Globalization;
using UnityEditor.VFX.Model;
using UnityEditor.VFX.Serialization;
using UnityEngine.VFX;

namespace UnityEditor.VFX.Generation;

/// <summary>
/// Lowers the executable Initialize subset to a backend-neutral, versioned runtime program.
/// The HLSL compiler remains the shader-source authority; this program is the native dispatch ABI.
/// Unsupported linked slot/activation expressions fail import instead of silently producing a
/// different particle result.
/// </summary>
internal static class VfxInitializeRuntimeKernelCompiler
{
    internal static VFXRuntimeInitializeKernelData Compile(
        VfxTypedGraph graph,
        long contextId,
        uint particleCapacity,
        IReadOnlyDictionary<string, VFXRuntimeAttributeData> sourceAttributes)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));
        if (particleCapacity == 0) throw new ArgumentOutOfRangeException(nameof(particleCapacity));
        VfxContextKernelCompilation compilation = VfxContextKernelCompiler.Compile(graph, contextId);
        var attributes = new List<VFXRuntimeInitializeAttributeData>(compilation.StoredAttributes.Count);
        var targetOffsets = new Dictionary<string, int>(StringComparer.Ordinal);
        int attributeOffsetWords = 0;
        foreach (VfxAttributeDefinition attribute in compilation.StoredAttributes)
        {
            VFXRuntimeValueType type = RuntimeType(attribute.ValueType);
            int wordCount = VFXRuntimeAssetData.WordCount(type);
            targetOffsets.Add(attribute.Name, attributeOffsetWords);
            attributes.Add(new VFXRuntimeInitializeAttributeData(
                new VFXRuntimeAttributeData(attribute.Name, type, attributeOffsetWords, wordCount),
                ParseDefaultWords(attribute)));
            attributeOffsetWords = checked(attributeOffsetWords + wordCount);
        }

        IReadOnlyDictionary<long, VfxSerializedAttributeUsage> usages = VfxAttributeUsageSet.Create(graph).Usages
            .Where(usage => usage.Model.ScriptType.TypeName is "SetAttribute" or "SetCustomAttribute")
            .ToDictionary(usage => usage.Model.FileId);
        var operations = new List<VFXRuntimeInitializeOperationData>();
        foreach (long blockId in compilation.CompiledBlockIds)
        {
            VfxModel block = graph.ModelsByFileId[blockId];
            if (!usages.TryGetValue(blockId, out VfxSerializedAttributeUsage? usage))
                throw new NotSupportedException(
                    $"VFX Initialize runtime program does not support block '{blockId}' type '{block.ScriptType.TypeName}'.");
            RejectDynamicActivation(graph, block);
            AppendOperations(graph, block, usage, targetOffsets, sourceAttributes, operations);
        }

        AppendSystemOperation("particleId", VFXRuntimeInitializeValueSource.ParticleId);
        AppendSystemOperation("seed", VFXRuntimeInitializeValueSource.Seed);
        AppendSystemOperation("spawnIndex", VFXRuntimeInitializeValueSource.SpawnIndex);

        int sourceStrideWords = sourceAttributes.Count == 0
            ? 1
            : sourceAttributes.Values.Max(attribute => checked(attribute.OffsetWords + attribute.SizeWords));
        int spawnCountSourceOffsetWords = -1;
        if (sourceAttributes.TryGetValue("spawnCount", out VFXRuntimeAttributeData spawnCount))
        {
            if (spawnCount.ValueType != VFXRuntimeValueType.Float || spawnCount.SizeWords != 1)
                throw new InvalidDataException(
                    "VFX runtime spawnCount Event attribute must be a scalar float.");
            spawnCountSourceOffsetWords = spawnCount.OffsetWords;
        }
        return new VFXRuntimeInitializeKernelData(
            contextId,
            particleCapacity,
            attributeOffsetWords,
            sourceStrideWords,
            compilation.UsesDeadList,
            attributes,
            operations,
            spawnCountSourceOffsetWords);

        void AppendSystemOperation(string attributeName, VFXRuntimeInitializeValueSource valueSource)
        {
            if (!targetOffsets.TryGetValue(attributeName, out int targetOffset)) return;
            operations.Add(new VFXRuntimeInitializeOperationData(
                targetOffset, -1, VFXRuntimeValueType.UInt32, valueSource,
                VFXRuntimeInitializeComposition.Overwrite,
                VFXRuntimeInitializeRandomMode.Off,
                Array.Empty<uint>(), Array.Empty<uint>(), FloatBits(1.0)));
        }
    }

    private static void AppendOperations(
        VfxTypedGraph graph,
        VfxModel block,
        VfxSerializedAttributeUsage usage,
        IReadOnlyDictionary<string, int> targetOffsets,
        IReadOnlyDictionary<string, VFXRuntimeAttributeData> sourceAttributes,
        ICollection<VFXRuntimeInitializeOperationData> operations)
    {
        VfxAttributeDefinition serialized = usage.IsCustom
            ? usage.Attributes.Single()
            : VfxAttributeCatalog.Find(usage.SerializedAttributeName);
        VfxSlotValue? valueA = null;
        VfxSlotValue? valueB = null;
        uint blendBits = FloatBits(1.0);
        if (usage.ValueSource == VfxAttributeValueSource.Slot)
        {
            if (usage.RandomMode == VfxAttributeRandomMode.Off)
            {
                string name = usage.IsCustom
                    ? "_" + PascalCase(usage.SerializedAttributeName)
                    : PascalCase(usage.SerializedAttributeName);
                valueA = ReadConstantInput(graph, block, name);
            }
            else
            {
                valueA = ReadConstantInput(graph, block, usage.IsCustom ? "Min" : "A");
                valueB = ReadConstantInput(graph, block, usage.IsCustom ? "Max" : "B");
            }
        }
        if (usage.Composition == VfxAttributeComposition.Blend)
        {
            VfxSlotValue blend = ReadConstantInput(graph, block, "Blend");
            if (blend.Kind != VfxSlotValueKind.Float || blend.Scalar is null)
                throw new InvalidDataException($"VFX Initialize block '{block.FileId}' Blend must be a scalar float.");
            blendBits = FloatBits(blend.Scalar.Value);
        }

        for (int index = 0; index < usage.Attributes.Count; index++)
        {
            VfxAttributeDefinition target = usage.Attributes[index];
            if (!targetOffsets.TryGetValue(target.Name, out int targetOffset))
                throw new InvalidDataException(
                    $"VFX Initialize runtime target '{target.Name}' is absent from the stored particle layout.");
            VFXRuntimeValueType valueType = RuntimeType(target.ValueType);
            int sourceOffset = -1;
            IReadOnlyList<uint> wordsA = Array.Empty<uint>();
            IReadOnlyList<uint> wordsB = Array.Empty<uint>();
            VFXRuntimeInitializeValueSource valueSource;
            if (usage.ValueSource == VfxAttributeValueSource.Source)
            {
                if (!sourceAttributes.TryGetValue(target.Name, out VFXRuntimeAttributeData source) ||
                    source.ValueType != valueType)
                    throw new InvalidDataException(
                        $"VFX Initialize source attribute '{target.Name}' is absent from the runtime Event record layout.");
                valueSource = VFXRuntimeInitializeValueSource.Source;
                sourceOffset = source.OffsetWords;
            }
            else
            {
                valueSource = VFXRuntimeInitializeValueSource.Constant;
                int? selectedComponent = serialized.Variadic == VfxAttributeVariadic.True ? index : null;
                wordsA = EncodeSlotValue(valueA!, valueType, selectedComponent);
                if (valueB is not null) wordsB = EncodeSlotValue(valueB, valueType, selectedComponent);
            }
            operations.Add(new VFXRuntimeInitializeOperationData(
                targetOffset,
                sourceOffset,
                valueType,
                valueSource,
                (VFXRuntimeInitializeComposition)usage.Composition,
                (VFXRuntimeInitializeRandomMode)usage.RandomMode,
                wordsA,
                wordsB,
                blendBits));
        }
    }

    private static void RejectDynamicActivation(VfxTypedGraph graph, VfxModel block)
    {
        long activationId = VfxYamlFields.ReadReference(block.Document.RawText, "m_ActivationSlot");
        if (activationId == 0) return;
        if (!graph.ModelsByFileId.TryGetValue(activationId, out VfxModel? activation) ||
            activation.LinkedSlotIds.Count != 0)
            throw new NotSupportedException(
                $"VFX Initialize runtime program does not yet support linked activation for block '{block.FileId}'.");
    }

    internal static VfxSlotValue ReadConstantInput(VfxTypedGraph graph, VfxModel block, string name)
    {
        VfxModel? slot = block.InputSlotIds.Select(id => graph.ModelsByFileId[id])
            .FirstOrDefault(candidate => string.Equals(candidate.SlotProperty?.Name, name, StringComparison.Ordinal));
        if (slot?.SlotProperty is null)
            throw new InvalidDataException($"VFX Initialize block '{block.FileId}' is missing input '{name}'.");
        if (slot.LinkedSlotIds.Count != 0)
            throw new NotSupportedException(
                $"VFX Initialize runtime program does not yet support linked input '{name}' on block '{block.FileId}'.");
        return slot.SlotProperty.Value;
    }

    internal static IReadOnlyList<uint> EncodeSlotValue(
        VfxSlotValue value,
        VFXRuntimeValueType targetType,
        int? selectedComponent)
    {
        if (selectedComponent is not null)
        {
            if (targetType != VFXRuntimeValueType.Float ||
                selectedComponent.Value < 0 || selectedComponent.Value >= value.Components.Count)
                throw new InvalidDataException("VFX variadic Initialize input does not match its scalar target channel.");
            return new[] { FloatBits(value.Components[selectedComponent.Value]) };
        }
        return targetType switch
        {
            VFXRuntimeValueType.Boolean when value.Kind == VfxSlotValueKind.Boolean && value.Boolean is not null
                => new[] { value.Boolean.Value ? 1u : 0u },
            VFXRuntimeValueType.UInt32 when value.Kind == VfxSlotValueKind.UInt32 && value.UnsignedInteger is not null
                => new[] { checked((uint)value.UnsignedInteger.Value) },
            VFXRuntimeValueType.Int32 when value.Kind == VfxSlotValueKind.Int32 && value.SignedInteger is not null
                => new[] { unchecked((uint)checked((int)value.SignedInteger.Value)) },
            VFXRuntimeValueType.Float when value.Kind == VfxSlotValueKind.Float && value.Scalar is not null
                => new[] { FloatBits(value.Scalar.Value) },
            VFXRuntimeValueType.Float2 when value.Components.Count == 2
                => value.Components.Select(FloatBits).ToArray(),
            VFXRuntimeValueType.Float3 when value.Components.Count == 3
                => value.Components.Select(FloatBits).ToArray(),
            VFXRuntimeValueType.Float4 when value.Components.Count == 4
                => value.Components.Select(FloatBits).ToArray(),
            _ => throw new InvalidDataException(
                $"VFX Initialize input kind '{value.Kind}' does not match runtime type '{targetType}'.")
        };
    }

    internal static IReadOnlyList<uint> ParseDefaultWords(VfxAttributeDefinition attribute)
    {
        string expression = attribute.DefaultHlsl.Trim();
        if (attribute.ValueType == VfxAttributeValueType.Boolean)
            return new[] { string.Equals(expression, "true", StringComparison.Ordinal) ? 1u : 0u };
        string values = expression;
        int open = expression.IndexOf('(');
        if (open >= 0)
        {
            int close = expression.LastIndexOf(')');
            if (close <= open) throw new InvalidDataException($"Invalid VFX default '{expression}'.");
            values = expression.Substring(open + 1, close - open - 1);
        }
        string[] parts = values.Split(',').Select(part => part.Trim()).ToArray();
        if (parts.Length != attribute.ComponentCount)
            throw new InvalidDataException($"VFX default '{expression}' has the wrong component count.");
        return attribute.ValueType switch
        {
            VfxAttributeValueType.UInt32 => parts.Select(part =>
                uint.Parse(part.TrimEnd('u', 'U'), NumberStyles.Integer, CultureInfo.InvariantCulture)).ToArray(),
            VfxAttributeValueType.Int32 => parts.Select(part =>
                unchecked((uint)int.Parse(part, NumberStyles.Integer, CultureInfo.InvariantCulture))).ToArray(),
            _ => parts.Select(part => FloatBits(double.Parse(
                part, NumberStyles.Float, CultureInfo.InvariantCulture))).ToArray()
        };
    }

    internal static VFXRuntimeValueType RuntimeType(VfxAttributeValueType type) => type switch
    {
        VfxAttributeValueType.Boolean => VFXRuntimeValueType.Boolean,
        VfxAttributeValueType.UInt32 => VFXRuntimeValueType.UInt32,
        VfxAttributeValueType.Int32 => VFXRuntimeValueType.Int32,
        VfxAttributeValueType.Float => VFXRuntimeValueType.Float,
        VfxAttributeValueType.Float2 => VFXRuntimeValueType.Float2,
        VfxAttributeValueType.Float3 => VFXRuntimeValueType.Float3,
        VfxAttributeValueType.Float4 => VFXRuntimeValueType.Float4,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    internal static uint FloatBits(double value)
    {
        float converted = checked((float)value);
        if (float.IsNaN(converted) || float.IsInfinity(converted))
            throw new InvalidDataException("VFX Initialize runtime operands must be finite.");
        return unchecked((uint)BitConverter.SingleToInt32Bits(converted));
    }

    internal static string PascalCase(string name)
        => char.ToUpperInvariant(name[0]) + name.Substring(1);
}
