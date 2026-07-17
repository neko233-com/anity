using System.Text.RegularExpressions;
using System.Text.Json;
using UnityEditor.VFX.Model;
using UnityEditor.VFX.Serialization;
using UnityEngine;
using UnityEngine.VFX;

namespace UnityEditor.VFX.Generation;

/// <summary>
/// Produces the checksummed, versioned runtime contract consumed by VisualEffectAsset.
/// Descriptor ordering follows serialized graph order so identical graphs produce identical bytes.
/// </summary>
internal static class VfxRuntimeAssetCompiler
{
    private static readonly Regex GeneratedSuffix = new(
        @" \(([0-9])*\)$",
        RegexOptions.CultureInvariant);

    internal static byte[] Compile(VfxTypedGraph graph)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));
        VfxContextSchema schema = VfxContextSchema.Create(graph);
        VFXRuntimeExposedPropertyData[] exposedProperties = CompileExposedProperties(graph);
        var eventAttributes = new List<(string Name, VFXRuntimeValueType Type)>();
        var eventAttributeTypes = new Dictionary<string, VFXRuntimeValueType>(StringComparer.Ordinal);
        // Unity's VFXExpressionGraph always reserves spawnCount as the first
        // global Event attribute. Native Initialize uses it to build the
        // inclusive per-event spawn prefix sum.
        AddGlobalAttribute(
            eventAttributes, eventAttributeTypes, "spawnCount", VFXRuntimeValueType.Float);

        foreach (VfxContextDescriptor init in schema.Contexts.Where(context =>
                     context.ContextType == VfxContextType.Init && context.Model.ChildrenIds.Count != 0))
        {
            VfxContextKernelCompilation compilation = VfxContextKernelCompiler.Compile(graph, init.Model.FileId);
            foreach (VfxAttributeLayoutField field in compilation.SourceAttributeLayout)
                AddGlobalAttribute(eventAttributes, eventAttributeTypes, field.Name, RuntimeType(field.HlslType));
        }

        CompiledSystems compiledSystems = CompileSystems(graph, schema);
        VFXRuntimeUpdateKernelData[] updateKernels = CompileUpdateKernels(
            graph, schema, compiledSystems.SystemsByDataId);
        VFXRuntimePlanarOutputData[] planarOutputs = CompilePlanarOutputs(
            graph, schema, compiledSystems.SystemsByDataId);
        var outputEvents = new List<VFXRuntimeOutputEventData>();
        var compiledOutputNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (VfxContextDescriptor output in schema.Contexts.Where(context =>
                     context.ContextType == VfxContextType.OutputEvent))
        {
            string eventName = output.EventName!;
            if (!compiledOutputNames.Add(eventName)) continue;
            VfxContextKernelCompilation compilation = VfxContextKernelCompiler.Compile(graph, output.Model.FileId);
            VfxEventAttributeLayout layout = compilation.CreateOutputEventRecordLayout();
            VFXRuntimeAttributeData[] outputAttributes = layout.Fields.Select(field =>
            {
                VFXRuntimeValueType type = RuntimeType(field.ValueType);
                AddGlobalAttribute(eventAttributes, eventAttributeTypes, field.Name, type);
                return new VFXRuntimeAttributeData(
                    field.Name,
                    type,
                    field.ElementOffsetWords,
                    field.SizeWords);
            }).ToArray();
            outputEvents.Add(new VFXRuntimeOutputEventData(
                eventName,
                compilation.OutputEventContextIds.ToArray(),
                compilation.OutputEventBufferMappings.Select(mapping =>
                    new VFXRuntimeOutputEventMapping(mapping.Name, mapping.SourceSpawnerContextId)).ToArray(),
                outputAttributes,
                layout.StructureSizeWords));
        }

        VfxAttributeUsageSet attributeUsages = VfxAttributeUsageSet.Create(graph);
        foreach (VfxSerializedAttributeUsage usage in attributeUsages.Usages.Where(candidate =>
                     candidate.Model.ScriptType.TypeName == "VFXSpawnerSetAttribute"))
        {
            foreach (VfxAttributeDefinition attribute in usage.Attributes)
                AddGlobalAttribute(eventAttributes, eventAttributeTypes, attribute.Name, RuntimeType(attribute.ValueType));
        }

        var packedAttributes = new List<VFXRuntimeAttributeData>(eventAttributes.Count);
        int offsetWords = 0;
        foreach ((string name, VFXRuntimeValueType type) in eventAttributes)
        {
            int sizeWords = VFXRuntimeAssetData.WordCount(type);
            packedAttributes.Add(new VFXRuntimeAttributeData(name, type, offsetWords, sizeWords));
            offsetWords = checked(offsetWords + sizeWords);
        }

        IReadOnlyDictionary<string, VFXRuntimeAttributeData> packedAttributesByName = packedAttributes
            .ToDictionary(attribute => attribute.Name, StringComparer.Ordinal);
        VFXRuntimeInputEventData[] inputEventDispatches = CompileInputEventDispatches(
            graph, schema, compiledSystems.SystemsByDataId, packedAttributesByName);
        string[] inputEvents = inputEventDispatches.Select(inputEvent => inputEvent.Name).ToArray();
        VFXRuntimeSpawnerProgramData[] spawnerPrograms = CompileSpawnerPrograms(
            graph, schema, compiledSystems.SystemsByDataId, packedAttributesByName,
            attributeUsages.Usages.ToDictionary(usage => usage.Model.FileId), offsetWords);

        var runtimeData = new VFXRuntimeAssetData(
            packedAttributes,
            inputEvents,
            inputEventDispatches,
            compiledSystems.Systems,
            outputEvents)
        {
            SpawnerPrograms = spawnerPrograms,
            ExposedProperties = exposedProperties,
            UpdateKernels = updateKernels,
            PlanarOutputs = planarOutputs
        };
        return runtimeData.Serialize();
    }

    private static VFXRuntimePlanarOutputData[] CompilePlanarOutputs(
        VfxTypedGraph graph,
        VfxContextSchema schema,
        IReadOnlyDictionary<long, VFXRuntimeSystemData> systemsByDataId)
    {
        var outputs = new List<VFXRuntimePlanarOutputData>();
        foreach (VfxContextDescriptor context in schema.Contexts.Where(candidate =>
                     candidate.ContextType == VfxContextType.Output &&
                     candidate.Model.ScriptType.TypeName == "VFXPlanarPrimitiveOutput"))
        {
            if (context.Data is null ||
                !systemsByDataId.TryGetValue(context.Data.Model.FileId, out VFXRuntimeSystemData system) ||
                system.Kind != VFXRuntimeSystemKind.Particle)
                throw new InvalidDataException(
                    $"VFX Planar Output context '{context.Model.FileId}' cannot resolve its particle system.");
            VfxContextKernelCompilation compilation;
            try
            {
                compilation = VfxContextKernelCompiler.Compile(graph, context.Model.FileId);
            }
            catch (NotSupportedException)
            {
                // Preserve official VFX Graph output metadata even while an output-specific block,
                // Shader Graph pass, or geometry path still lacks executable shader generation.
                // RuntimeExecutable remains false, so the product runtime cannot mistake this
                // compatibility descriptor for a renderable program.
                compilation = VfxContextKernelCompiler.DescribePlanarOutput(graph, context.Model.FileId);
            }
            VfxPlanarRenderState renderState = compilation.PlanarRenderState
                ?? throw new InvalidDataException(
                    $"VFX Planar Output context '{context.Model.FileId}' has no render state.");
            if (compilation.AttributeLayout.Count != compilation.StoredAttributes.Count ||
                compilation.AttributeStrideBytes <= 0 ||
                compilation.AttributeStrideBytes % sizeof(uint) != 0)
                throw new InvalidDataException(
                    $"VFX Planar Output context '{context.Model.FileId}' has an invalid particle layout.");
            VFXRuntimeAttributeData[] attributes = compilation.StoredAttributes
                .Select((attribute, index) =>
                {
                    VfxAttributeLayoutField field = compilation.AttributeLayout[index];
                    if (field.OffsetBytes % sizeof(uint) != 0 || field.SizeBytes % sizeof(uint) != 0)
                        throw new InvalidDataException(
                            $"VFX Planar Output context '{context.Model.FileId}' has an unaligned attribute '{attribute.Name}'.");
                    return new VFXRuntimeAttributeData(
                        attribute.Name,
                        RuntimeType(attribute.ValueType),
                        field.OffsetBytes / sizeof(uint),
                        field.SizeBytes / sizeof(uint));
                })
                .ToArray();
            int primitiveType = compilation.VerticesPerParticle switch
            {
                3 => 0,
                4 => 1,
                8 => 2,
                _ => throw new InvalidDataException(
                    $"VFX Planar Output context '{context.Model.FileId}' has unsupported geometry.")
            };
            outputs.Add(new VFXRuntimePlanarOutputData(
                context.Model.FileId,
                system.Name,
                primitiveType,
                compilation.VerticesPerParticle,
                compilation.IndexPattern.ToArray(),
                VfxYamlFields.ReadInt32(context.Model.Document.RawText, "uvMode") ?? 0,
                (int)renderState.BlendMode,
                (int)renderState.CullMode,
                renderState.ZWrite,
                (int)renderState.ZTest,
                renderState.AlphaClipping,
                renderState.RenderQueue,
                renderState.RequiresSorting,
                renderState.IndirectDraw,
                compilation.RuntimeExecutable,
                attributes,
                compilation.AttributeStrideBytes / sizeof(uint)));
        }
        return outputs.ToArray();
    }

    private static VFXRuntimeUpdateKernelData[] CompileUpdateKernels(
        VfxTypedGraph graph,
        VfxContextSchema schema,
        IReadOnlyDictionary<long, VFXRuntimeSystemData> systemsByDataId)
    {
        var kernels = new List<VFXRuntimeUpdateKernelData>();
        foreach (VfxContextDescriptor context in schema.Contexts.Where(candidate =>
                     candidate.ContextType == VfxContextType.Update))
        {
            if (context.Data is null ||
                !systemsByDataId.TryGetValue(context.Data.Model.FileId, out VFXRuntimeSystemData system) ||
                system.Kind is not (VFXRuntimeSystemKind.Particle or VFXRuntimeSystemKind.ParticleStrip))
                throw new InvalidDataException(
                    $"VFX Update context '{context.Model.FileId}' cannot resolve its particle system.");
            VFXRuntimeUpdateKernelData? kernel = VfxUpdateRuntimeKernelCompiler.Compile(
                graph, context.Model.FileId, system.Name, system.Capacity);
            if (kernel is not null) kernels.Add(kernel);
        }
        return kernels.ToArray();
    }

    private static VFXRuntimeSpawnerProgramData[] CompileSpawnerPrograms(
        VfxTypedGraph graph,
        VfxContextSchema schema,
        IReadOnlyDictionary<long, VFXRuntimeSystemData> systemsByDataId,
        IReadOnlyDictionary<string, VFXRuntimeAttributeData> sourceAttributes,
        IReadOnlyDictionary<long, VfxSerializedAttributeUsage> attributeUsages,
        int eventStrideWords)
    {
        var programs = new List<VFXRuntimeSpawnerProgramData>();
        foreach (VfxContextDescriptor context in schema.Contexts.Where(candidate =>
                     candidate.ContextType == VfxContextType.Spawner))
        {
            if (context.Data is null ||
                !systemsByDataId.TryGetValue(context.Data.Model.FileId, out VFXRuntimeSystemData spawnSystem))
                throw new InvalidDataException(
                    $"VFX Spawner '{context.Model.FileId}' cannot resolve its runtime spawn system.");

            VFXRuntimeSpawnerValueMode loopDurationMode = LoopMode(context.Model, "loopDuration");
            VFXRuntimeSpawnerValueMode loopCountMode = LoopMode(context.Model, "loopCount");
            VFXRuntimeSpawnerValueMode delayBeforeMode = DelayMode(context.Model, "delayBeforeLoop");
            VFXRuntimeSpawnerValueMode delayAfterMode = DelayMode(context.Model, "delayAfterLoop");
            (float loopDurationMin, float loopDurationMax) = ReadSpawnerFloatOperand(
                graph, context.Model, "LoopDuration", loopDurationMode);
            (double loopCountMin, double loopCountMax) = ReadSpawnerLoopCountOperand(
                graph, context.Model, loopCountMode);
            (float delayBeforeMin, float delayBeforeMax) = ReadSpawnerFloatOperand(
                graph, context.Model, "DelayBeforeLoop", delayBeforeMode);
            (float delayAfterMin, float delayAfterMax) = ReadSpawnerFloatOperand(
                graph, context.Model, "DelayAfterLoop", delayAfterMode);

            var activeBlocks = new List<VfxModel>();
            foreach (long childId in context.Model.ChildrenIds)
            {
                VfxModel block = graph.ModelsByFileId[childId];
                if (VfxYamlFields.ReadInt32(block.Document.RawText, "m_Disabled") == 1 ||
                    !IsActivationEnabled(graph, block))
                    continue;
                activeBlocks.Add(block);
            }
            bool hasSpawnerTask = activeBlocks.Any(block => block.ScriptType.TypeName is
                "VFXSpawnerConstantRate" or "VFXSpawnerVariableRate" or "VFXSpawnerBurst" ||
                block.ScriptType.TypeName == "VFXSpawnerCustomWrapper" ||
                block.ScriptType.TypeName == "VFXSpawnerSetAttribute" && block.InputSlotIds.Count > 0);
            // Synthetic schema-only SetAttribute models without operands remain
            // outside the executable runtime contract. Authored SetAttribute-only
            // Spawners are native programs, including Output Event-only graphs.
            if (!hasSpawnerTask) continue;
            var blocks = activeBlocks.Select(block =>
                CompileSpawnerBlock(graph, block, sourceAttributes, attributeUsages)).ToList();

            VFXRuntimeSpawnerControlData[] controls = graph.FlowEdges
                .Where(edge => edge.TargetContextId == context.Model.FileId)
                .Select(edge =>
                {
                    VfxContextDescriptor source = schema.ContextsById[edge.SourceContextId];
                    if (source.ContextType != VfxContextType.Event)
                        throw new NotSupportedException(
                            $"VFX Spawner '{context.Model.FileId}' has a non-Event input context '{source.Model.FileId}'.");
                    return new VFXRuntimeSpawnerControlData(source.EventName!, edge.TargetSlotIndex);
                })
                .Distinct()
                .ToArray();
            if (controls.Length == 0)
                controls = new[]
                {
                    new VFXRuntimeSpawnerControlData(VisualEffectAsset.PlayEventName, 0),
                    new VFXRuntimeSpawnerControlData(VisualEffectAsset.StopEventName, 1)
                };

            var outputs = new List<VFXRuntimeSpawnerOutputData>();
            foreach (VfxFlowEdge edge in graph.FlowEdges.Where(candidate =>
                         candidate.SourceContextId == context.Model.FileId))
            {
                VfxContextDescriptor output = schema.ContextsById[edge.TargetContextId];
                if (output.ContextType == VfxContextType.OutputEvent)
                    continue;
                if (output.ContextType != VfxContextType.Init || output.Data is null ||
                    !systemsByDataId.TryGetValue(output.Data.Model.FileId, out VFXRuntimeSystemData particleSystem))
                    throw new NotSupportedException(
                        $"VFX Spawner '{context.Model.FileId}' output must directly target an Initialize context.");
                VFXRuntimeInitializeKernelData? kernel = output.Model.ChildrenIds.Count == 0
                    ? null
                    : VfxInitializeRuntimeKernelCompiler.Compile(
                        graph, output.Model.FileId, particleSystem.Capacity, sourceAttributes);
                outputs.Add(new VFXRuntimeSpawnerOutputData(
                    output.Model.FileId, particleSystem.Name, kernel));
            }

            programs.Add(new VFXRuntimeSpawnerProgramData(
                context.Model.FileId,
                spawnSystem.Name,
                loopDurationMode,
                loopCountMode,
                delayBeforeMode,
                delayAfterMode,
                loopDurationMin, loopDurationMax,
                loopCountMin, loopCountMax,
                delayBeforeMin, delayBeforeMax,
                delayAfterMin, delayAfterMax,
                controls,
                outputs.ToArray(),
                blocks.ToArray())
            {
                EventStrideWords = blocks.Any(block => block.Kind is
                        VFXRuntimeSpawnerBlockKind.SetAttribute or
                        VFXRuntimeSpawnerBlockKind.CustomCallback)
                    ? eventStrideWords
                    : 0
            });
        }
        return programs.ToArray();
    }

    private static VFXRuntimeSpawnerBlockData CompileSpawnerBlock(
        VfxTypedGraph graph,
        VfxModel block,
        IReadOnlyDictionary<string, VFXRuntimeAttributeData> sourceAttributes,
        IReadOnlyDictionary<long, VfxSerializedAttributeUsage> attributeUsages)
    {
        return block.ScriptType.TypeName switch
        {
            "VFXSpawnerConstantRate" => ConstantRateBlock(graph, block),
            "VFXSpawnerVariableRate" => VariableRateBlock(graph, block),
            "VFXSpawnerBurst" => BurstBlock(graph, block),
            "VFXSpawnerSetAttribute" => SetAttributeBlock(
                graph, block, sourceAttributes, attributeUsages),
            "VFXSpawnerCustomWrapper" => CustomCallbackBlock(graph, block),
            _ => throw new NotSupportedException(
                $"VFX Spawner block '{block.ScriptType.TypeName}' ({block.FileId}) has no runtime opcode.")
        };
    }

    private static VFXRuntimeSpawnerBlockData CustomCallbackBlock(
        VfxTypedGraph graph,
        VfxModel block)
    {
        string callbackType = VfxYamlFields.ReadDescendantFoldedScalar(
                                  block.Document.RawText,
                                  "m_customType",
                                  "m_SerializableType")
                              ?? throw new InvalidDataException(
                                  $"VFX Custom Spawner Callback block '{block.FileId}' has no serialized type.");
        if (string.IsNullOrWhiteSpace(callbackType))
            throw new InvalidDataException(
                $"VFX Custom Spawner Callback block '{block.FileId}' has an empty serialized type.");
        VFXRuntimeSpawnerExpressionValueData[] values = block.InputSlotIds
            .Select(id => graph.ModelsByFileId[id])
            .Select(slot =>
            {
                VfxSlotProperty property = slot.SlotProperty
                    ?? throw new InvalidDataException(
                        $"VFX Custom Spawner Callback block '{block.FileId}' has an invalid input slot.");
                VFXRuntimeValueType runtimeType = CallbackRuntimeType(
                    property.Value.Kind, block.FileId, property.Name);
                var value = new VFXRuntimeSpawnerExpressionValueData(
                    property.Name,
                    runtimeType,
                    RuntimeWords(property.Value, runtimeType, block.FileId, property.Name));
                if (slot.LinkedSlotIds.Count == 0) return value;
                if (slot.LinkedSlotIds.Count != 1)
                    throw new InvalidDataException(
                        $"VFX Custom Spawner Callback block '{block.FileId}' input '{property.Name}' has multiple links.");
                VfxModel source = graph.ModelsByFileId[slot.LinkedSlotIds[0]];
                if (source.OwnerId == 0 ||
                    !graph.ModelsByFileId.TryGetValue(source.OwnerId, out VfxModel? owner) ||
                    owner.ScriptType.TypeName != "VFXParameter" ||
                    VfxYamlFields.ReadInt32(owner.Document.RawText, "m_Exposed") != 1)
                    return value with
                    {
                        Expression = VfxRuntimeExpressionCompiler.CompileInput(graph, slot.FileId)
                    };
                VfxSlotProperty sourceProperty = source.SlotProperty
                    ?? throw new InvalidDataException(
                        $"VFX Custom Spawner Callback block '{block.FileId}' input '{property.Name}' source has no typed value.");
                if (CallbackRuntimeType(sourceProperty.Value.Kind, block.FileId, property.Name) != runtimeType)
                    throw new InvalidDataException(
                        $"VFX Custom Spawner Callback block '{block.FileId}' input '{property.Name}' source type does not match.");
                string exposedName = VfxYamlFields.ReadString(owner.Document.RawText, "m_ExposedName")?.Trim()
                    ?? string.Empty;
                if (exposedName.Length == 0)
                    throw new InvalidDataException(
                        $"VFX exposed parameter '{owner.FileId}' has no exposed name.");
                return value with { SourcePropertyName = exposedName };
            })
            .ToArray();
        return new VFXRuntimeSpawnerBlockData(
            block.FileId, VFXRuntimeSpawnerBlockKind.CustomCallback,
            0f, 0f, 0f, 0f, false)
        {
            CallbackTypeName = callbackType,
            CallbackValues = values
        };
    }

    internal static VFXRuntimeValueType CallbackRuntimeType(
        VfxSlotValueKind kind,
        long blockId,
        string inputName) => kind switch
        {
            VfxSlotValueKind.Boolean => VFXRuntimeValueType.Boolean,
            VfxSlotValueKind.UInt32 => VFXRuntimeValueType.UInt32,
            VfxSlotValueKind.Int32 => VFXRuntimeValueType.Int32,
            VfxSlotValueKind.Float => VFXRuntimeValueType.Float,
            VfxSlotValueKind.Float2 => VFXRuntimeValueType.Float2,
            VfxSlotValueKind.Float3 => VFXRuntimeValueType.Float3,
            VfxSlotValueKind.Float4 => VFXRuntimeValueType.Float4,
            VfxSlotValueKind.Transform => VFXRuntimeValueType.Matrix4x4,
            _ => throw new NotSupportedException(
                $"VFX Custom Spawner Callback block '{blockId}' input '{inputName}' type '{kind}' is not yet exportable.")
        };

    private static VFXRuntimeExposedPropertyData[] CompileExposedProperties(VfxTypedGraph graph)
    {
        var properties = new List<VFXRuntimeExposedPropertyData>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (VfxModel parameter in graph.Parameters.Where(candidate =>
                     candidate.ScriptType.TypeName == "VFXParameter" &&
                     VfxYamlFields.ReadInt32(candidate.Document.RawText, "m_Exposed") == 1))
        {
            string name = VfxYamlFields.ReadString(parameter.Document.RawText, "m_ExposedName")?.Trim()
                ?? string.Empty;
            if (name.Length == 0 || !names.Add(name))
                throw new InvalidDataException(
                    $"VFX exposed parameter '{parameter.FileId}' has an empty or duplicate name.");
            if (parameter.OutputSlotIds.Count != 1)
                throw new NotSupportedException(
                    $"VFX exposed parameter '{name}' must have exactly one root output slot.");
            VfxModel slot = graph.ModelsByFileId[parameter.OutputSlotIds[0]];
            VfxSlotProperty property = slot.SlotProperty
                ?? throw new InvalidDataException($"VFX exposed parameter '{name}' has no typed value.");
            VFXRuntimeValueType type = CallbackRuntimeType(property.Value.Kind, parameter.FileId, name);
            properties.Add(new VFXRuntimeExposedPropertyData(
                name,
                type,
                RuntimeWords(property.Value, type, parameter.FileId, name)));
        }
        return properties.ToArray();
    }

    private static VFXRuntimeSpawnerBlockData SetAttributeBlock(
        VfxTypedGraph graph,
        VfxModel block,
        IReadOnlyDictionary<string, VFXRuntimeAttributeData> sourceAttributes,
        IReadOnlyDictionary<long, VfxSerializedAttributeUsage> attributeUsages)
    {
        if (!attributeUsages.TryGetValue(block.FileId, out VfxSerializedAttributeUsage? usage) ||
            usage.Attributes.Count == 0)
            throw new InvalidDataException($"VFX Set SpawnEvent Attribute block '{block.FileId}' has no attribute contract.");
        VfxAttributeDefinition serialized = VfxAttributeCatalog.Find(usage.SerializedAttributeName);
        VFXRuntimeValueType valueType = RuntimeType(serialized.ValueType);
        int wordCount = VFXRuntimeAssetData.WordCount(valueType);
        VFXRuntimeAttributeData[] fields = usage.Attributes.Select(attribute =>
            sourceAttributes.TryGetValue(attribute.Name, out VFXRuntimeAttributeData field)
                ? field
                : throw new InvalidDataException(
                    $"VFX Set SpawnEvent Attribute block '{block.FileId}' cannot resolve '{attribute.Name}'."))
            .ToArray();
        int targetOffset = fields[0].OffsetWords;
        if (fields.Sum(field => field.SizeWords) != wordCount ||
            fields.SelectMany(field => Enumerable.Range(field.OffsetWords, field.SizeWords))
                .Where((offset, index) => offset != targetOffset + index).Any())
            throw new NotSupportedException(
                $"VFX Set SpawnEvent Attribute block '{block.FileId}' requires a contiguous event layout.");

        string firstSlot = usage.RandomMode == VfxAttributeRandomMode.Off
            ? usage.SerializedAttributeName
            : "Min";
        VfxSlotValue first = ResolveConstantSlot(graph, block, firstSlot);
        VfxSlotValue second = usage.RandomMode == VfxAttributeRandomMode.Off
            ? first
            : ResolveConstantSlot(graph, block, "Max");
        uint[] valueA = RuntimeWords(first, valueType, block.FileId, firstSlot);
        uint[] valueB = RuntimeWords(second, valueType, block.FileId,
            usage.RandomMode == VfxAttributeRandomMode.Off ? firstSlot : "Max");
        if (usage.RandomMode != VfxAttributeRandomMode.Off &&
            valueType is not (VFXRuntimeValueType.Float or VFXRuntimeValueType.Float2 or
                VFXRuntimeValueType.Float3 or VFXRuntimeValueType.Float4))
            throw new NotSupportedException(
                $"VFX Set SpawnEvent Attribute block '{block.FileId}' random mode requires a floating-point attribute.");

        return new VFXRuntimeSpawnerBlockData(
            block.FileId, VFXRuntimeSpawnerBlockKind.SetAttribute,
            0f, 0f, 0f, 0f, false)
        {
            TargetOffsetWords = targetOffset,
            TargetValueType = valueType,
            RandomMode = (VFXRuntimeInitializeRandomMode)usage.RandomMode,
            ValueA = valueA,
            ValueB = valueB
        };
    }

    internal static uint[] RuntimeWords(
        VfxSlotValue value,
        VFXRuntimeValueType type,
        long ownerId,
        string slotName)
    {
        static uint FloatWord(double component, long ownerId, string slotName)
        {
            float value = (float)component;
            if (!float.IsFinite(value))
                throw new InvalidDataException(
                    $"VFX Spawner '{ownerId}' input '{slotName}' must be finite.");
            return unchecked((uint)BitConverter.SingleToInt32Bits(value));
        }

        static uint[] TransformWords(VfxSlotValue value, long ownerId, string slotName)
        {
            Matrix4x4 matrix = Matrix4x4.TRS(
                new Vector3((float)value.Components[0], (float)value.Components[1], (float)value.Components[2]),
                Quaternion.Euler((float)value.Components[3], (float)value.Components[4], (float)value.Components[5]),
                new Vector3((float)value.Components[6], (float)value.Components[7], (float)value.Components[8]));
            return Enumerable.Range(0, 16)
                .Select(index => FloatWord(matrix[index], ownerId, slotName))
                .ToArray();
        }

        return type switch
        {
            VFXRuntimeValueType.Boolean when value.Kind == VfxSlotValueKind.Boolean && value.Boolean is not null
                => new[] { value.Boolean.Value ? 1u : 0u },
            VFXRuntimeValueType.UInt32 when value.Kind == VfxSlotValueKind.UInt32 && value.UnsignedInteger is not null &&
                                            value.UnsignedInteger <= uint.MaxValue
                => new[] { (uint)value.UnsignedInteger.Value },
            VFXRuntimeValueType.Int32 when value.Kind == VfxSlotValueKind.Int32 && value.SignedInteger is not null &&
                                           value.SignedInteger is >= int.MinValue and <= int.MaxValue
                => new[] { unchecked((uint)(int)value.SignedInteger.Value) },
            VFXRuntimeValueType.Float when value.Kind == VfxSlotValueKind.Float && value.Scalar is not null
                => new[] { FloatWord(value.Scalar.Value, ownerId, slotName) },
            VFXRuntimeValueType.Float2 when value.Components.Count >= 2
                => value.Components.Take(2).Select(component => FloatWord(component, ownerId, slotName)).ToArray(),
            VFXRuntimeValueType.Float3 when value.Components.Count >= 3
                => value.Components.Take(3).Select(component => FloatWord(component, ownerId, slotName)).ToArray(),
            VFXRuntimeValueType.Float4 when value.Components.Count >= 4
                => value.Components.Take(4).Select(component => FloatWord(component, ownerId, slotName)).ToArray(),
            VFXRuntimeValueType.Matrix4x4 when value.Kind == VfxSlotValueKind.Transform &&
                                               value.Components.Count == 9
                => TransformWords(value, ownerId, slotName),
            _ => throw new InvalidDataException(
                $"VFX Spawner '{ownerId}' input '{slotName}' does not match runtime type '{type}'.")
        };
    }

    private static VFXRuntimeSpawnerBlockData ConstantRateBlock(VfxTypedGraph graph, VfxModel block)
    {
        float rate = ReadScalarSlot(graph, block, "Rate");
        return new VFXRuntimeSpawnerBlockData(
            block.FileId, VFXRuntimeSpawnerBlockKind.ConstantRate,
            rate, rate, 0f, 0f, false);
    }

    private static VFXRuntimeSpawnerBlockData VariableRateBlock(VfxTypedGraph graph, VfxModel block)
    {
        (float rateMin, float rateMax) = ReadRangeSlot(graph, block, "Rate");
        (float periodMin, float periodMax) = ReadRangeSlot(graph, block, "Period");
        return new VFXRuntimeSpawnerBlockData(
            block.FileId, VFXRuntimeSpawnerBlockKind.VariableRate,
            rateMin, rateMax, periodMin, periodMax, false);
    }

    private static VFXRuntimeSpawnerBlockData BurstBlock(VfxTypedGraph graph, VfxModel block)
    {
        int repeat = VfxYamlFields.ReadInt32(block.Document.RawText, "repeat") ?? 0;
        int spawnMode = VfxYamlFields.ReadInt32(block.Document.RawText, "spawnMode") ?? 0;
        int delayMode = VfxYamlFields.ReadInt32(block.Document.RawText, "delayMode") ?? 0;
        if (repeat is < 0 or > 1 || spawnMode is < 0 or > 1 || delayMode is < 0 or > 1)
            throw new InvalidDataException($"VFX Burst block '{block.FileId}' has invalid serialized modes.");
        (float valueMin, float valueMax) = spawnMode == 0
            ? ScalarRange(ReadScalarSlot(graph, block, "Count"))
            : ReadRangeSlot(graph, block, "Count");
        (float periodMin, float periodMax) = delayMode == 0
            ? ScalarRange(ReadScalarSlot(graph, block, "Delay"))
            : ReadRangeSlot(graph, block, "Delay");
        return new VFXRuntimeSpawnerBlockData(
            block.FileId, VFXRuntimeSpawnerBlockKind.Burst,
            valueMin, valueMax, periodMin, periodMax, repeat == 1);
    }

    private static bool IsActivationEnabled(VfxTypedGraph graph, VfxModel block)
    {
        if (block.ActivationSlotId == 0) return true;
        VfxModel slot = graph.ModelsByFileId[block.ActivationSlotId];
        if (slot.LinkedSlotIds.Count != 0)
            throw new NotSupportedException(
                $"VFX Spawner block '{block.FileId}' has a linked activation expression.");
        return slot.SlotProperty?.Value.Boolean
               ?? throw new InvalidDataException($"VFX Spawner block '{block.FileId}' activation is not Boolean.");
    }

    private static float ReadScalarSlot(VfxTypedGraph graph, VfxModel owner, string name)
    {
        VfxSlotValue value = ResolveConstantSlot(graph, owner, name);
        if (value.Kind != VfxSlotValueKind.Float || value.Scalar is null)
            throw new InvalidDataException($"VFX Spawner '{owner.FileId}' input '{name}' must be a float.");
        return CheckedFloat(value.Scalar.Value, owner.FileId, name);
    }

    private static (float Min, float Max) ReadRangeSlot(VfxTypedGraph graph, VfxModel owner, string name)
    {
        VfxSlotValue value = ResolveConstantSlot(graph, owner, name);
        if (value.Kind != VfxSlotValueKind.Float2 || value.Components.Count != 2)
            throw new InvalidDataException($"VFX Spawner '{owner.FileId}' input '{name}' must be a float2.");
        float min = CheckedFloat(value.Components[0], owner.FileId, name);
        float max = CheckedFloat(value.Components[1], owner.FileId, name);
        if (min > max)
            throw new InvalidDataException($"VFX Spawner '{owner.FileId}' input '{name}' range is reversed.");
        return (min, max);
    }

    private static int ReadIntSlot(VfxTypedGraph graph, VfxModel owner, string name)
    {
        VfxSlotValue value = ResolveConstantSlot(graph, owner, name);
        if (value.Kind != VfxSlotValueKind.Int32 || value.SignedInteger is null ||
            value.SignedInteger < 0 || value.SignedInteger > int.MaxValue)
            throw new InvalidDataException(
                $"VFX Spawner '{owner.FileId}' input '{name}' must be a non-negative Int32.");
        return (int)value.SignedInteger.Value;
    }

    private static (float Min, float Max) ReadSpawnerFloatOperand(
        VfxTypedGraph graph,
        VfxModel context,
        string slotName,
        VFXRuntimeSpawnerValueMode mode)
        => mode switch
        {
            VFXRuntimeSpawnerValueMode.Disabled or VFXRuntimeSpawnerValueMode.Infinite => (0f, 0f),
            VFXRuntimeSpawnerValueMode.Constant => ScalarRange(ReadScalarSlot(graph, context, slotName)),
            VFXRuntimeSpawnerValueMode.Random => ReadRangeSlot(graph, context, slotName),
            _ => throw new InvalidDataException(
                $"VFX Spawner '{context.FileId}' has invalid '{slotName}' mode.")
        };

    private static (double Min, double Max) ReadSpawnerLoopCountOperand(
        VfxTypedGraph graph,
        VfxModel context,
        VFXRuntimeSpawnerValueMode mode)
    {
        if (mode == VFXRuntimeSpawnerValueMode.Infinite) return (0d, 0d);
        if (mode == VFXRuntimeSpawnerValueMode.Constant)
        {
            int value = ReadIntSlot(graph, context, "LoopCount");
            return (value, value);
        }
        if (mode != VFXRuntimeSpawnerValueMode.Random)
            throw new InvalidDataException($"VFX Spawner '{context.FileId}' has invalid LoopCount mode.");
        (float minimum, float maximum) = ReadRangeSlot(graph, context, "LoopCount");
        const float largestSafeInt32Float = 2147483520f;
        if (maximum > largestSafeInt32Float)
            throw new InvalidDataException(
                $"VFX Spawner '{context.FileId}' random LoopCount exceeds the Float-to-Int32 range.");
        return (minimum, maximum);
    }

    private static VfxSlotValue ResolveConstantSlot(VfxTypedGraph graph, VfxModel owner, string name)
    {
        VfxModel slot = owner.InputSlotIds.Select(id => graph.ModelsByFileId[id]).SingleOrDefault(candidate =>
                            string.Equals(candidate.SlotProperty?.Name, name, StringComparison.Ordinal))
                        ?? throw new InvalidDataException($"VFX Spawner '{owner.FileId}' is missing input '{name}'.");
        if (slot.LinkedSlotIds.Count != 0)
            throw new NotSupportedException(
                $"VFX Spawner '{owner.FileId}' input '{name}' has a linked runtime expression.");
        return slot.SlotProperty!.Value;
    }

    private static float CheckedFloat(double value, long ownerId, string name)
    {
        float result = (float)value;
        if (!float.IsFinite(result) || result < 0f)
            throw new InvalidDataException($"VFX Spawner '{ownerId}' input '{name}' must be finite and non-negative.");
        return result;
    }

    private static (float Min, float Max) ScalarRange(float value) => (value, value);

    private static VFXRuntimeSpawnerValueMode LoopMode(VfxModel context, string field)
        => (VfxYamlFields.ReadInt32(context.Document.RawText, field) ?? 0) switch
        {
            0 => VFXRuntimeSpawnerValueMode.Infinite,
            1 => VFXRuntimeSpawnerValueMode.Constant,
            2 => VFXRuntimeSpawnerValueMode.Random,
            _ => throw new InvalidDataException($"VFX Spawner '{context.FileId}' has invalid {field} mode.")
        };

    private static VFXRuntimeSpawnerValueMode DelayMode(VfxModel context, string field)
        => (VfxYamlFields.ReadInt32(context.Document.RawText, field) ?? 0) switch
        {
            0 => VFXRuntimeSpawnerValueMode.Disabled,
            1 => VFXRuntimeSpawnerValueMode.Constant,
            2 => VFXRuntimeSpawnerValueMode.Random,
            _ => throw new InvalidDataException($"VFX Spawner '{context.FileId}' has invalid {field} mode.")
        };

    internal static void CompileInto(VfxTypedGraph graph, VisualEffectAsset asset)
    {
        if (asset is null) throw new ArgumentNullException(nameof(asset));
        asset.ImportRuntimeData(Compile(graph));
    }

    private static CompiledSystems CompileSystems(VfxTypedGraph graph, VfxContextSchema schema)
    {
        var systems = new List<VFXRuntimeSystemData>();
        var systemsByDataId = new Dictionary<long, VFXRuntimeSystemData>();
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (VfxDataDescriptor data in schema.Data)
        {
            VFXRuntimeSystemKind kind;
            uint capacity;
            string desiredName;
            switch (data.DataType)
            {
                case VfxDataType.Particle:
                    kind = VFXRuntimeSystemKind.Particle;
                    capacity = data.Capacity!.Value;
                    desiredName = VfxYamlFields.ReadString(data.Model.Document.RawText, "title") ?? string.Empty;
                    break;
                case VfxDataType.ParticleStrip:
                    kind = VFXRuntimeSystemKind.ParticleStrip;
                    capacity = data.Capacity!.Value;
                    desiredName = VfxYamlFields.ReadString(data.Model.Document.RawText, "title") ?? string.Empty;
                    break;
                case VfxDataType.SpawnEvent:
                    kind = VFXRuntimeSystemKind.Spawn;
                    capacity = 0;
                    desiredName = SpawnerLabel(graph, data);
                    break;
                case VfxDataType.Mesh:
                    kind = VFXRuntimeSystemKind.Mesh;
                    capacity = 0;
                    desiredName = VfxYamlFields.ReadString(data.Model.Document.RawText, "title") ?? string.Empty;
                    break;
                default:
                    throw new NotSupportedException($"VFX runtime system data '{data.DataType}' is not supported.");
            }
            string systemName = UniqueSystemName(desiredName, usedNames);
            VFXRuntimeSystemData runtimeSystem = new(systemName, kind, capacity);
            if (kind is VFXRuntimeSystemKind.Particle or VFXRuntimeSystemKind.ParticleStrip &&
                TryCompileStaticBounds(graph, schema, data, out Vector3 center, out Vector3 size))
            {
                runtimeSystem = runtimeSystem with
                {
                    HasStaticBounds = true,
                    BoundsInWorldSpace = data.Space == VfxCoordinateSpace.World,
                    BoundsCenterX = center.x,
                    BoundsCenterY = center.y,
                    BoundsCenterZ = center.z,
                    BoundsSizeX = size.x,
                    BoundsSizeY = size.y,
                    BoundsSizeZ = size.z
                };
            }
            else if (kind is VFXRuntimeSystemKind.Particle or VFXRuntimeSystemKind.ParticleStrip &&
                     (data.NeedsComputeBounds || data.BoundsMode == VfxBoundsSettingMode.Automatic))
            {
                runtimeSystem = CompileAutomaticBounds(graph, schema, data, runtimeSystem);
            }
            systems.Add(runtimeSystem);
            systemsByDataId.Add(data.Model.FileId, runtimeSystem);
        }
        return new CompiledSystems(systems.ToArray(), systemsByDataId);
    }

    private static bool TryCompileStaticBounds(
        VfxTypedGraph graph,
        VfxContextSchema schema,
        VfxDataDescriptor data,
        out Vector3 center,
        out Vector3 size)
    {
        center = default;
        size = default;
        if (data.NeedsComputeBounds ||
            data.BoundsMode is not (VfxBoundsSettingMode.Recorded or VfxBoundsSettingMode.Manual))
            return false;
        VfxContextDescriptor? initialize = schema.Contexts.FirstOrDefault(context =>
            context.ContextType == VfxContextType.Init &&
            ReferenceEquals(context.Data?.Model, data.Model));
        if (initialize is null) return false;
        VfxModel? boundsSlot = FindInputSlot(graph, initialize.Model, "bounds");
        if (boundsSlot?.SlotProperty?.Value.Json is not JsonElement json ||
            boundsSlot.LinkedSlotIds.Count != 0 ||
            !TryReadVector3(json, "center", out center) ||
            !TryReadVector3(json, "size", out size) ||
            size.x < 0f || size.y < 0f || size.z < 0f)
        {
            center = default;
            size = default;
            return false;
        }
        if (data.BoundsMode == VfxBoundsSettingMode.Recorded)
        {
            VfxModel? paddingSlot = FindInputSlot(graph, initialize.Model, "boundsPadding");
            if (paddingSlot is not null)
            {
                if (paddingSlot.LinkedSlotIds.Count != 0 ||
                    paddingSlot.SlotProperty?.Value.Components is not { Count: >= 3 } components)
                {
                    center = default;
                    size = default;
                    return false;
                }
                Vector3 padding = new(
                    CheckedBoundsFloat(components[0]),
                    CheckedBoundsFloat(components[1]),
                    CheckedBoundsFloat(components[2]));
                if (padding.x < 0f || padding.y < 0f || padding.z < 0f)
                {
                    center = default;
                    size = default;
                    return false;
                }
                size += padding * 2f;
            }
        }
        return IsFinite(center) && IsFinite(size);
    }

    private static VFXRuntimeSystemData CompileAutomaticBounds(
        VfxTypedGraph graph,
        VfxContextSchema schema,
        VfxDataDescriptor data,
        VFXRuntimeSystemData system)
    {
        VfxContextDescriptor? initialize = schema.Contexts.FirstOrDefault(context =>
            context.ContextType == VfxContextType.Init &&
            ReferenceEquals(context.Data?.Model, data.Model));
        if (initialize is null)
            throw new InvalidDataException(
                $"VFX Automatic bounds system '{data.Model.FileId}' has no Initialize context.");
        VfxContextKernelCompilation compilation =
            VfxContextKernelCompiler.Compile(graph, initialize.Model.FileId);
        var offsets = new Dictionary<string, int>(StringComparer.Ordinal);
        int offsetWords = 0;
        foreach (VfxAttributeDefinition attribute in compilation.StoredAttributes)
        {
            offsets.Add(attribute.Name, offsetWords);
            offsetWords = checked(offsetWords + attribute.ComponentCount);
        }
        VfxAttributeDefinition? position = compilation.StoredAttributes.FirstOrDefault(
            attribute => string.Equals(attribute.Name, "position", StringComparison.Ordinal));
        if (position is null || position.ValueType != VfxAttributeValueType.Float3 ||
            !offsets.TryGetValue("position", out int positionOffset))
            throw new NotSupportedException(
                $"VFX Automatic bounds system '{data.Model.FileId}' requires a stored Float3 position attribute.");
        Vector3 padding = ReadBoundsPadding(graph, initialize.Model);
        return system with
        {
            HasAutomaticBounds = true,
            BoundsInWorldSpace = data.Space == VfxCoordinateSpace.World,
            PositionOffsetWords = positionOffset,
            AliveOffsetWords = Offset("alive"),
            SizeOffsetWords = Offset("size"),
            ScaleXOffsetWords = Offset("scaleX"),
            ScaleYOffsetWords = Offset("scaleY"),
            ScaleZOffsetWords = Offset("scaleZ"),
            AutomaticBoundsPaddingX = padding.x,
            AutomaticBoundsPaddingY = padding.y,
            AutomaticBoundsPaddingZ = padding.z
        };

        int Offset(string name) => offsets.TryGetValue(name, out int value) ? value : -1;
    }

    private static Vector3 ReadBoundsPadding(VfxTypedGraph graph, VfxModel initialize)
    {
        VfxModel? paddingSlot = FindInputSlot(graph, initialize, "boundsPadding");
        if (paddingSlot is null) return Vector3.zero;
        if (paddingSlot.LinkedSlotIds.Count != 0)
            throw new NotSupportedException(
                $"VFX Automatic bounds Initialize '{initialize.FileId}' has linked boundsPadding, which requires runtime expression evaluation.");
        if (paddingSlot.SlotProperty?.Value.Components is not { Count: >= 3 } components)
            throw new InvalidDataException(
                $"VFX Automatic bounds Initialize '{initialize.FileId}' has invalid boundsPadding.");
        Vector3 padding = new(
            CheckedBoundsFloat(components[0]),
            CheckedBoundsFloat(components[1]),
            CheckedBoundsFloat(components[2]));
        if (!IsFinite(padding) || padding.x < 0f || padding.y < 0f || padding.z < 0f)
            throw new InvalidDataException(
                $"VFX Automatic bounds Initialize '{initialize.FileId}' has negative or non-finite boundsPadding.");
        return padding;
    }

    private static VfxModel? FindInputSlot(VfxTypedGraph graph, VfxModel owner, string name)
        => owner.InputSlotIds.Select(id => graph.ModelsByFileId[id]).FirstOrDefault(slot =>
            string.Equals(slot.SlotProperty?.Name, name, StringComparison.Ordinal));

    private static bool TryReadVector3(JsonElement root, string propertyName, out Vector3 value)
    {
        value = default;
        if (!root.TryGetProperty(propertyName, out JsonElement item) ||
            item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("x", out JsonElement x) || !x.TryGetSingle(out float xv) ||
            !item.TryGetProperty("y", out JsonElement y) || !y.TryGetSingle(out float yv) ||
            !item.TryGetProperty("z", out JsonElement z) || !z.TryGetSingle(out float zv))
            return false;
        value = new Vector3(xv, yv, zv);
        return IsFinite(value);
    }

    private static float CheckedBoundsFloat(double value)
    {
        float result = (float)value;
        return float.IsFinite(result) ? result : float.NaN;
    }

    private static bool IsFinite(Vector3 value)
        => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

    private static VFXRuntimeInputEventData[] CompileInputEventDispatches(
        VfxTypedGraph graph,
        VfxContextSchema schema,
        IReadOnlyDictionary<long, VFXRuntimeSystemData> systemsByDataId,
        IReadOnlyDictionary<string, VFXRuntimeAttributeData> sourceAttributes)
    {
        var names = new List<string>();
        var contextsByName = new Dictionary<string, List<VfxContextDescriptor>>(StringComparer.Ordinal);
        foreach (VfxContextDescriptor context in schema.Contexts.Where(candidate =>
                     candidate.ContextType == VfxContextType.Event))
        {
            string name = context.EventName!;
            if (!contextsByName.TryGetValue(name, out List<VfxContextDescriptor>? contexts))
            {
                contexts = new List<VfxContextDescriptor>();
                contextsByName.Add(name, contexts);
                names.Add(name);
            }
            contexts.Add(context);
        }

        var result = new List<VFXRuntimeInputEventData>(names.Count);
        foreach (string name in names)
        {
            var targets = new List<VFXRuntimeInputEventTargetData>();
            var targetKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (VfxContextDescriptor eventContext in contextsByName[name])
                CollectInputTargets(
                    graph,
                    schema,
                    systemsByDataId,
                    sourceAttributes,
                    eventContext.Model.FileId,
                    Array.Empty<long>(),
                    Array.Empty<string>(),
                    targets,
                    targetKeys,
                    name);
            result.Add(new VFXRuntimeInputEventData(name, targets.ToArray()));
        }
        return result.ToArray();
    }

    private static void CollectInputTargets(
        VfxTypedGraph graph,
        VfxContextSchema schema,
        IReadOnlyDictionary<long, VFXRuntimeSystemData> systemsByDataId,
        IReadOnlyDictionary<string, VFXRuntimeAttributeData> sourceAttributes,
        long sourceContextId,
        IReadOnlyList<long> spawnerContextIds,
        IReadOnlyList<string> spawnSystemNames,
        List<VFXRuntimeInputEventTargetData> targets,
        HashSet<string> targetKeys,
        string eventName)
    {
        foreach (VfxFlowEdge edge in graph.FlowEdges.Where(candidate =>
                     candidate.SourceContextId == sourceContextId))
        {
            VfxContextDescriptor target = schema.ContextsById[edge.TargetContextId];
            if (target.ContextType == VfxContextType.Spawner)
            {
                if (target.Data is null ||
                    !systemsByDataId.TryGetValue(target.Data.Model.FileId, out VFXRuntimeSystemData spawnSystem))
                    throw new InvalidDataException(
                        $"VFX input event '{eventName}' cannot resolve Spawner '{target.Model.FileId}' runtime system.");
                CollectInputTargets(
                    graph,
                    schema,
                    systemsByDataId,
                    sourceAttributes,
                    target.Model.FileId,
                    spawnerContextIds.Append(target.Model.FileId).ToArray(),
                    spawnSystemNames.Append(spawnSystem.Name).ToArray(),
                    targets,
                    targetKeys,
                    eventName);
                continue;
            }
            if (target.ContextType == VfxContextType.Init)
            {
                if (target.Data is null ||
                    !systemsByDataId.TryGetValue(target.Data.Model.FileId, out VFXRuntimeSystemData particleSystem))
                    throw new InvalidDataException(
                        $"VFX input event '{eventName}' cannot resolve Initialize '{target.Model.FileId}' particle system.");
                string key = target.Model.FileId + ":" + string.Join(",", spawnerContextIds);
                if (targetKeys.Add(key))
                {
                    VFXRuntimeInitializeKernelData? kernel = target.Model.ChildrenIds.Count == 0
                        ? null
                        : VfxInitializeRuntimeKernelCompiler.Compile(
                            graph, target.Model.FileId, particleSystem.Capacity, sourceAttributes);
                    targets.Add(new VFXRuntimeInputEventTargetData(
                        target.Model.FileId,
                        particleSystem.Name,
                        spawnerContextIds.ToArray(),
                        spawnSystemNames.ToArray(),
                        kernel));
                }
                continue;
            }
            throw new NotSupportedException(
                $"VFX input event '{eventName}' flow reaches unsupported context '{target.Model.FileId}' of type '{target.ContextType}'.");
        }
    }

    private static string SpawnerLabel(VfxTypedGraph graph, VfxDataDescriptor data)
    {
        VfxModel? owner = data.Model.OwnerIds
            .Select(id => graph.ModelsByFileId[id])
            .FirstOrDefault(model => model.ScriptType.TypeName == "VFXBasicSpawner");
        if (owner is null) return string.Empty;
        return VfxYamlFields.ReadString(owner.Document.RawText, "m_Label")
               ?? owner.SerializedName;
    }

    private static string UniqueSystemName(string desiredName, HashSet<string> usedNames)
    {
        string baseName = GeneratedSuffix.Replace(desiredName.Trim(), string.Empty);
        if (baseName.Length == 0) baseName = "System";
        string candidate = baseName;
        int suffix = 1;
        while (!usedNames.Add(candidate)) candidate = $"{baseName} ({suffix++})";
        return candidate;
    }

    private static void AddGlobalAttribute(
        List<(string Name, VFXRuntimeValueType Type)> ordered,
        Dictionary<string, VFXRuntimeValueType> types,
        string name,
        VFXRuntimeValueType type)
    {
        if (types.TryGetValue(name, out VFXRuntimeValueType existing))
        {
            if (existing != type)
                throw new InvalidDataException($"VFX event attribute '{name}' has conflicting runtime types.");
            return;
        }
        types.Add(name, type);
        ordered.Add((name, type));
    }

    private static VFXRuntimeValueType RuntimeType(VfxAttributeValueType type) => type switch
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

    private static VFXRuntimeValueType RuntimeType(string hlslType) => hlslType switch
    {
        "bool" => VFXRuntimeValueType.Boolean,
        "uint" => VFXRuntimeValueType.UInt32,
        "int" => VFXRuntimeValueType.Int32,
        "float" => VFXRuntimeValueType.Float,
        "float2" => VFXRuntimeValueType.Float2,
        "float3" => VFXRuntimeValueType.Float3,
        "float4" => VFXRuntimeValueType.Float4,
        _ => throw new InvalidDataException($"VFX source attribute type '{hlslType}' cannot be exported to runtime.")
    };

    private sealed record CompiledSystems(
        VFXRuntimeSystemData[] Systems,
        IReadOnlyDictionary<long, VFXRuntimeSystemData> SystemsByDataId);
}
