using UnityEditor.VFX.Model;
using UnityEditor.VFX.Serialization;
using UnityEngine.VFX;

namespace UnityEditor.VFX.Generation;

/// <summary>
/// Lowers the executable Update subset to ordered backend-neutral IR consumed
/// by the native particle store. Unsupported dynamic inputs fail import rather
/// than silently diverging from the HLSL kernel.
/// </summary>
internal static class VfxUpdateRuntimeKernelCompiler
{
    internal static VFXRuntimeUpdateKernelData? Compile(
        VfxTypedGraph graph,
        long contextId,
        string particleSystemName,
        uint particleCapacity)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));
        if (string.IsNullOrEmpty(particleSystemName))
            throw new ArgumentException("Particle system name cannot be empty.", nameof(particleSystemName));
        if (particleCapacity == 0) throw new ArgumentOutOfRangeException(nameof(particleCapacity));
        VfxContextKernelCompilation compilation = VfxContextKernelCompiler.Compile(graph, contextId);
        var offsets = new Dictionary<string, int>(StringComparer.Ordinal);
        int strideWords = 0;
        foreach (VfxAttributeDefinition attribute in compilation.StoredAttributes)
        {
            offsets.Add(attribute.Name, strideWords);
            strideWords = checked(strideWords + attribute.ComponentCount);
        }
        IReadOnlyDictionary<long, VfxSerializedAttributeUsage> usages =
            VfxAttributeUsageSet.Create(graph).Usages
                .Where(usage => usage.Model.ScriptType.TypeName is "SetAttribute" or "SetCustomAttribute")
                .ToDictionary(usage => usage.Model.FileId);
        VfxModel context = graph.ModelsByFileId[contextId];
        HashSet<long> compiledBlockIds = compilation.CompiledBlockIds.ToHashSet();
        var operations = new List<VFXRuntimeUpdateOperationData>();
        var writtenAttributes = new HashSet<string>(StringComparer.Ordinal);
        foreach (long childId in context.ChildrenIds)
        {
            if (!compiledBlockIds.Contains(childId)) continue;
            VfxModel block = graph.ModelsByFileId[childId];
            RejectDynamicActivation(graph, block);
            switch (block.ScriptType.TypeName)
            {
                case "SetAttribute":
                case "SetCustomAttribute":
                    AppendSetAttribute(graph, block, usages[childId], offsets, operations, writtenAttributes);
                    break;
                case "Gravity":
                    operations.Add(IntegrateConstant(
                        Offset("velocity"), VFXRuntimeValueType.Float3,
                        Words(Input(graph, block, "Force"), VFXRuntimeValueType.Float3)));
                    writtenAttributes.Add("velocity");
                    break;
                case "Force":
                    AppendForce(graph, block, offsets, operations);
                    writtenAttributes.Add("velocity");
                    break;
                case "Drag":
                    AppendDrag(graph, block, offsets, operations);
                    writtenAttributes.Add("velocity");
                    break;
                default:
                    throw new NotSupportedException(
                        $"VFX Update runtime program does not support block '{childId}' type '{block.ScriptType.TypeName}'.");
            }
        }

        int integration = ReadEnumSetting(context, "integration", 0, 0, 1);
        int angularIntegration = ReadEnumSetting(context, "angularIntegration", 0, 0, 1);
        bool ageParticles = ReadBooleanSetting(context, "ageParticles", true);
        bool reapParticles = ReadBooleanSetting(context, "reapParticles", true);
        bool skipZeroDelta = ReadBooleanSetting(context, "skipZeroDeltaUpdate", false);
        if (integration == 0 && offsets.ContainsKey("position") && offsets.ContainsKey("velocity"))
            operations.Add(IntegrateSource(
                Offset("position"), Offset("velocity"), VFXRuntimeValueType.Float3));
        if (angularIntegration == 0)
        {
            AddAngular("X");
            AddAngular("Y");
            AddAngular("Z");
        }
        if (offsets.ContainsKey("age"))
        {
            if (ageParticles)
                operations.Add(IntegrateConstant(
                    Offset("age"), VFXRuntimeValueType.Float,
                    new[] { VfxInitializeRuntimeKernelCompiler.FloatBits(1.0) }));
            if (reapParticles && offsets.ContainsKey("lifetime") && offsets.ContainsKey("alive"))
                operations.Add(Operation(
                    VFXRuntimeUpdateOperationKind.Reap,
                    Offset("alive"), Offset("age"), Offset("lifetime"),
                    VFXRuntimeValueType.Boolean));
        }
        if (operations.Count == 0)
            return null;
        return new VFXRuntimeUpdateKernelData(
            contextId,
            particleSystemName,
            particleCapacity,
            strideWords,
            compilation.UsesDeadList,
            skipZeroDelta,
            OptionalOffset("alive"),
            compilation.UsesRandom ? Offset("seed") : -1,
            operations);

        int Offset(string name)
            => offsets.TryGetValue(name, out int value)
                ? value
                : throw new InvalidDataException(
                    $"VFX Update context '{contextId}' requires stored attribute '{name}'.");

        int OptionalOffset(string name) => offsets.TryGetValue(name, out int value) ? value : -1;

        void AddAngular(string axis)
        {
            string angularVelocity = "angularVelocity" + axis;
            if (!offsets.ContainsKey("angle" + axis) || !offsets.ContainsKey(angularVelocity)) return;
            operations.Add(IntegrateSource(
                Offset("angle" + axis), Offset(angularVelocity), VFXRuntimeValueType.Float));
        }
    }

    private static void AppendSetAttribute(
        VfxTypedGraph graph,
        VfxModel block,
        VfxSerializedAttributeUsage usage,
        IReadOnlyDictionary<string, int> offsets,
        ICollection<VFXRuntimeUpdateOperationData> operations,
        ISet<string> writtenAttributes)
    {
        VfxAttributeDefinition serialized = usage.IsCustom
            ? usage.Attributes.Single()
            : VfxAttributeCatalog.Find(usage.SerializedAttributeName);
        VfxSlotValue? valueA = null;
        VfxSlotValue? valueB = null;
        uint blendBits = VfxInitializeRuntimeKernelCompiler.FloatBits(1.0);
        if (usage.ValueSource == VfxAttributeValueSource.Slot)
        {
            if (usage.RandomMode == VfxAttributeRandomMode.Off)
            {
                string name = usage.IsCustom
                    ? "_" + VfxInitializeRuntimeKernelCompiler.PascalCase(usage.SerializedAttributeName)
                    : VfxInitializeRuntimeKernelCompiler.PascalCase(usage.SerializedAttributeName);
                valueA = Input(graph, block, name);
            }
            else
            {
                valueA = Input(graph, block, usage.IsCustom ? "Min" : "A");
                valueB = Input(graph, block, usage.IsCustom ? "Max" : "B");
            }
        }
        if (usage.Composition == VfxAttributeComposition.Blend)
        {
            VfxSlotValue blend = Input(graph, block, "Blend");
            if (blend.Kind != VfxSlotValueKind.Float || blend.Scalar is null)
                throw new InvalidDataException(
                    $"VFX Update block '{block.FileId}' Blend must be a scalar float.");
            blendBits = VfxInitializeRuntimeKernelCompiler.FloatBits(blend.Scalar.Value);
        }
        for (int index = 0; index < usage.Attributes.Count; index++)
        {
            VfxAttributeDefinition target = usage.Attributes[index];
            if (!offsets.TryGetValue(target.Name, out int targetOffset))
                throw new InvalidDataException(
                    $"VFX Update target '{target.Name}' is absent from the stored particle layout.");
            VFXRuntimeValueType type = VfxInitializeRuntimeKernelCompiler.RuntimeType(target.ValueType);
            bool sourceSnapshot = usage.ValueSource == VfxAttributeValueSource.Source;
            int? selectedComponent = serialized.Variadic == VfxAttributeVariadic.True ? index : null;
            IReadOnlyList<uint> wordsA = sourceSnapshot
                ? Array.Empty<uint>()
                : VfxInitializeRuntimeKernelCompiler.EncodeSlotValue(valueA!, type, selectedComponent);
            IReadOnlyList<uint> wordsB = valueB is null
                ? Array.Empty<uint>()
                : VfxInitializeRuntimeKernelCompiler.EncodeSlotValue(valueB, type, selectedComponent);
            operations.Add(new VFXRuntimeUpdateOperationData(
                VFXRuntimeUpdateOperationKind.SetAttribute,
                targetOffset,
                sourceSnapshot ? targetOffset : -1,
                -1, -1, -1,
                type,
                (VFXRuntimeInitializeComposition)usage.Composition,
                (VFXRuntimeInitializeRandomMode)usage.RandomMode,
                sourceSnapshot,
                wordsA,
                wordsB,
                blendBits));
            writtenAttributes.Add(target.Name);
        }
    }

    private static void AppendForce(
        VfxTypedGraph graph,
        VfxModel block,
        IReadOnlyDictionary<string, int> offsets,
        ICollection<VFXRuntimeUpdateOperationData> operations)
    {
        int mode = VfxYamlFields.ReadInt32(block.Document.RawText, "Mode") ?? 0;
        int velocity = RequiredOffset(offsets, "velocity", block.FileId);
        int mass = RequiredOffset(offsets, "mass", block.FileId);
        if (mode == 0)
        {
            operations.Add(Operation(
                VFXRuntimeUpdateOperationKind.Force,
                velocity, mass, -1, VFXRuntimeValueType.Float3,
                Words(Input(graph, block, "Force"), VFXRuntimeValueType.Float3)));
        }
        else if (mode == 1)
        {
            operations.Add(Operation(
                VFXRuntimeUpdateOperationKind.RelativeForce,
                velocity, mass, -1, VFXRuntimeValueType.Float3,
                Words(Input(graph, block, "Velocity"), VFXRuntimeValueType.Float3),
                Words(Input(graph, block, "Drag"), VFXRuntimeValueType.Float)));
        }
        else
        {
            throw new InvalidDataException(
                $"VFX Force block '{block.FileId}' has invalid Mode '{mode}'.");
        }
    }

    private static void AppendDrag(
        VfxTypedGraph graph,
        VfxModel block,
        IReadOnlyDictionary<string, int> offsets,
        ICollection<VFXRuntimeUpdateOperationData> operations)
    {
        bool useParticleSize = ReadBooleanSetting(block, "UseParticleSize", false);
        int size = useParticleSize ? RequiredOffset(offsets, "size", block.FileId) : -1;
        int scaleX = useParticleSize ? RequiredOffset(offsets, "scaleX", block.FileId) : -1;
        int scaleY = useParticleSize ? RequiredOffset(offsets, "scaleY", block.FileId) : -1;
        operations.Add(new VFXRuntimeUpdateOperationData(
            VFXRuntimeUpdateOperationKind.Drag,
            RequiredOffset(offsets, "velocity", block.FileId),
            RequiredOffset(offsets, "mass", block.FileId),
            size, scaleX, scaleY,
            VFXRuntimeValueType.Float3,
            VFXRuntimeInitializeComposition.Overwrite,
            VFXRuntimeInitializeRandomMode.Off,
            false,
            Words(Input(graph, block, "dragCoefficient"), VFXRuntimeValueType.Float),
            Array.Empty<uint>(),
            VfxInitializeRuntimeKernelCompiler.FloatBits(1.0)));
    }

    private static VFXRuntimeUpdateOperationData IntegrateSource(
        int target,
        int source,
        VFXRuntimeValueType type)
        => Operation(VFXRuntimeUpdateOperationKind.Integrate, target, source, -1, type);

    private static VFXRuntimeUpdateOperationData IntegrateConstant(
        int target,
        VFXRuntimeValueType type,
        IReadOnlyList<uint> values)
        => Operation(VFXRuntimeUpdateOperationKind.Integrate, target, -1, -1, type, values);

    private static VFXRuntimeUpdateOperationData Operation(
        VFXRuntimeUpdateOperationKind kind,
        int target,
        int sourceA,
        int sourceB,
        VFXRuntimeValueType type,
        IReadOnlyList<uint>? valueA = null,
        IReadOnlyList<uint>? valueB = null)
        => new(
            kind, target, sourceA, sourceB, -1, -1, type,
            VFXRuntimeInitializeComposition.Overwrite,
            VFXRuntimeInitializeRandomMode.Off,
            false,
            valueA ?? Array.Empty<uint>(),
            valueB ?? Array.Empty<uint>(),
            VfxInitializeRuntimeKernelCompiler.FloatBits(1.0));

    private static VfxSlotValue Input(VfxTypedGraph graph, VfxModel block, string name)
        => VfxInitializeRuntimeKernelCompiler.ReadConstantInput(graph, block, name);

    private static IReadOnlyList<uint> Words(VfxSlotValue value, VFXRuntimeValueType type)
        => VfxInitializeRuntimeKernelCompiler.EncodeSlotValue(value, type, null);

    private static int RequiredOffset(
        IReadOnlyDictionary<string, int> offsets,
        string name,
        long blockId)
        => offsets.TryGetValue(name, out int offset)
            ? offset
            : throw new InvalidDataException(
                $"VFX Update block '{blockId}' requires stored attribute '{name}'.");

    private static void RejectDynamicActivation(VfxTypedGraph graph, VfxModel block)
    {
        long activationId = VfxYamlFields.ReadReference(block.Document.RawText, "m_ActivationSlot");
        if (activationId == 0) return;
        if (!graph.ModelsByFileId.TryGetValue(activationId, out VfxModel? activation) ||
            activation.LinkedSlotIds.Count != 0)
            throw new NotSupportedException(
                $"VFX Update runtime program does not support linked activation on block '{block.FileId}'.");
    }

    private static int ReadEnumSetting(
        VfxModel model,
        string name,
        int defaultValue,
        params int[] validValues)
    {
        int value = VfxYamlFields.ReadInt32(model.Document.RawText, name) ?? defaultValue;
        if (!validValues.Contains(value))
            throw new InvalidDataException(
                $"VFX model '{model.FileId}' field {name} has invalid value '{value}'.");
        return value;
    }

    private static bool ReadBooleanSetting(VfxModel model, string name, bool defaultValue)
    {
        int value = VfxYamlFields.ReadInt32(model.Document.RawText, name) ?? (defaultValue ? 1 : 0);
        if (value is not (0 or 1))
            throw new InvalidDataException(
                $"VFX model '{model.FileId}' field {name} must be 0 or 1.");
        return value == 1;
    }
}
