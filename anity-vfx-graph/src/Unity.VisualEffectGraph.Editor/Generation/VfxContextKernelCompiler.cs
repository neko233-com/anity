using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using UnityEditor.VFX.Model;
using UnityEditor.VFX.Serialization;

namespace UnityEditor.VFX.Generation;

/// <summary>
/// Compiles VFX Graph 14 CPU Output Event descriptors plus executable Initialize, Update,
/// and Planar Output programs. The supported subset follows Unity's context-specific
/// rules. Unsupported semantics fail before shader or runtime metadata is emitted.
/// </summary>
internal static class VfxContextKernelCompiler
{
    private const int ThreadGroupSize = 64;

    internal static VfxContextKernelCompilation Compile(VfxTypedGraph graph, long contextId)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));
        VfxContextSchema schema = VfxContextSchema.Create(graph);
        if (!schema.ContextsById.TryGetValue(contextId, out VfxContextDescriptor? context))
            throw new KeyNotFoundException($"VFX context kernel cannot resolve context '{contextId}'.");
        if (context.ContextType == VfxContextType.OutputEvent)
            return CompileOutputEvent(graph, schema, context);
        if (context.ContextType == VfxContextType.Init && context.Task == VfxTaskKind.Initialize)
            return CompileInitialize(graph, schema, context);
        if (context.ContextType == VfxContextType.Output &&
            context.Model.ScriptType.TypeName == "VFXPlanarPrimitiveOutput")
            return CompilePlanarOutput(graph, schema, context);
        if (context.ContextType != VfxContextType.Update || context.Task != VfxTaskKind.Update)
            throw new NotSupportedException(
                $"VFX context compiler supports Output Event, Initialize, Update, and Planar Output contexts only; '{contextId}' is {context.ContextType}.");
        if (context.Data?.DataType is not (VfxDataType.Particle or VfxDataType.ParticleStrip))
            throw new NotSupportedException($"VFX Update context '{contextId}' requires particle data.");

        IReadOnlyDictionary<long, VfxSerializedAttributeUsage> usagesByModel =
            VfxAttributeUsageSet.Create(graph).Usages
                .Where(usage => usage.Model.ScriptType.TypeName is "SetAttribute" or "SetCustomAttribute")
                .ToDictionary(usage => usage.Model.FileId);
        var blocks = new List<CompiledBlock>();
        foreach (long childId in context.Model.ChildrenIds)
        {
            VfxModel child = graph.ModelsByFileId[childId];
            ActivationCompilation activation = CompileActivation(graph, child, ReadDisabled(child));
            if (!activation.IsIncluded) continue;
            if (child.Kind != VfxModelKind.Block)
                throw new NotSupportedException(
                    $"VFX Update kernel does not support enabled child '{child.FileId}' type '{child.ScriptType.TypeName}'.");
            blocks.Add(CompileBlock(graph, child, usagesByModel, activation));
        }

        VfxSerializedAttributeUsage[] selectedUsages = blocks
            .Where(block => block.Usage is not null)
            .Select(block => block.Usage!)
            .ToArray();
        bool usesRandom = selectedUsages.Any(usage => usage.RequiresRandomSeed);
        VfxSerializedAttributeUsage[] sourceUsages = selectedUsages
            .Where(usage => usage.ValueSource == VfxAttributeValueSource.Source)
            .ToArray();
        VfxExpressionAttributeDependency[] expressionDependencies = blocks
            .SelectMany(block => block.AttributeDependencies)
            .GroupBy(dependency => (dependency.Attribute.Name, dependency.Mode))
            .Select(group => group.First())
            .ToArray();
        var explicitAccesses = blocks.SelectMany(block => block.AttributeAccesses)
            .Concat(expressionDependencies
                .Where(dependency => dependency.Mode == VfxAttributeMode.Read)
                .Select(dependency => new AttributeAccess(dependency.Attribute, VfxAttributeMode.Read)))
            .ToArray();
        VfxAttributeDefinition[] storedAttributes = CollectDataWideStoredAttributes(
            graph, schema, context.Data, usagesByModel);
        UpdateSettings updateSettings = ReadUpdateSettings(context.Model);
        ImplicitUpdateCompilation implicitUpdate = CompileImplicitUpdate(
            explicitAccesses, storedAttributes, updateSettings);
        VfxAttributeDefinition[] currentAttributes = explicitAccesses
            .Concat(implicitUpdate.AttributeAccesses)
            .Select(access => access.Attribute)
            .Concat(usesRandom ? new[] { VfxAttributeCatalog.Find("seed") } : Array.Empty<VfxAttributeDefinition>())
            .ToArray();
        VfxAttributeDefinition[] sourceAttributes = sourceUsages.SelectMany(usage => usage.Attributes)
            .Concat(expressionDependencies
                .Where(dependency => dependency.Mode == VfxAttributeMode.ReadSource)
                .Select(dependency => dependency.Attribute))
            .ToArray();
        sourceAttributes = DistinctAttributes(sourceAttributes).ToArray();
        bool usesSource = sourceAttributes.Length != 0;
        bool usesAlive = currentAttributes.Any(attribute => attribute.Name == "alive");
        bool usesDeadList = usesAlive && context.Data.DataType == VfxDataType.Particle;
        var source = new StringBuilder();
        source.Append(VfxAttributeCodeGenerator.GenerateAttributeStructFromAttributes(storedAttributes))
            .Append('\n');
        if (usesSource)
            source.Append(VfxAttributeCodeGenerator.GenerateAttributeStructFromAttributes(
                    sourceAttributes,
                    "VFXSourceAttributes"))
                .Append('\n');
        source.Append("RWByteAddressBuffer attributeBuffer : register(u0);\n");
        AppendAttributeBufferHelpers(source, storedAttributes);
        if (usesDeadList)
            source.Append("RWStructuredBuffer<uint> deadListOut : register(u1);\n")
                .Append("RWStructuredBuffer<uint> deadListCount : register(u2);\n");
        source
            .Append("cbuffer AnityVfxDispatch : register(b0)\n{\n")
            .Append("    uint particleCount;\n")
            .Append("    float deltaTime;\n")
            .Append(usesDeadList ? "    uint deadListCapacity;\n" : string.Empty)
            .Append("    float4x4 localToWorld;\n")
            .Append("    float4x4 worldToLocal;\n")
            .Append("};\n\n");
        if (usesRandom) source.Append(RandomHelpers).Append('\n');
        source.Append("[numthreads(").Append(ThreadGroupSize).Append(", 1, 1)]\n")
            .Append("void Update(uint3 dispatchThreadId : SV_DispatchThreadID)\n{\n")
            .Append("    uint particleIndex = dispatchThreadId.x;\n")
            .Append("    if (particleIndex >= particleCount) return;\n")
            .Append("    VFXAttributes attributes = AnityVfxLoadAttributes(particleIndex);\n");
        if (usesAlive)
            source.Append("    if (!attributes.alive) return;\n");
        if (usesSource)
        {
            source.Append("    VFXSourceAttributes sourceAttributes = (VFXSourceAttributes)0;\n");
            HashSet<string> currentNames = currentAttributes
                .Select(attribute => attribute.Name)
                .ToHashSet(StringComparer.Ordinal);
            foreach (VfxAttributeDefinition attribute in DistinctAttributes(sourceAttributes))
                source.Append("    sourceAttributes.").Append(attribute.Name)
                    .Append(" = ")
                    .Append(currentNames.Contains(attribute.Name)
                        ? "attributes." + attribute.Name
                        : attribute.DefaultHlsl)
                    .Append(";\n");
        }
        if (usesRandom)
            source.Append("    uint randomState = attributes.seed ^ particleIndex;\n");
        string updateIndent = "    ";
        if (updateSettings.SkipZeroDeltaUpdate)
        {
            source.Append("    if (deltaTime != 0.0)\n")
                .Append("    {\n");
            updateIndent = "        ";
        }
        foreach (string statement in implicitUpdate.PreStatements)
            source.Append(updateIndent).Append(statement).Append('\n');
        foreach (CompiledBlock block in blocks)
        {
            source.Append(updateIndent).Append("{\n");
            foreach (string declaration in block.ActivationDeclarations)
                source.Append(updateIndent).Append("    ").Append(declaration).Append('\n');
            string bodyIndent = updateIndent + "    ";
            if (block.ActivationCondition is not null)
            {
                source.Append(updateIndent).Append("    if (").Append(block.ActivationCondition).Append(")\n")
                    .Append(updateIndent).Append("    {\n");
                bodyIndent = updateIndent + "        ";
            }
            if (block.Usage?.ValueSource == VfxAttributeValueSource.Source)
                source.Append(bodyIndent).Append(SourceValueDeclaration(block.Usage)).Append('\n');
            foreach (string declaration in block.InputDeclarations)
                source.Append(bodyIndent).Append(declaration).Append('\n');
            string statement = block.Statement;
            if (block.Usage?.RequiresRandomSeed == true) statement = ExpandRandomMacros(statement);
            foreach (string line in statement.Split('\n'))
                source.Append(bodyIndent).Append(line).Append('\n');
            if (block.ActivationCondition is not null)
                source.Append(updateIndent).Append("    }\n");
            source.Append(updateIndent).Append("}\n");
        }
        foreach (string statement in implicitUpdate.PostStatements)
            source.Append(updateIndent).Append(statement).Append('\n');
        if (usesRandom) source.Append(updateIndent).Append("attributes.seed = randomState;\n");
        if (updateSettings.SkipZeroDeltaUpdate) source.Append("    }\n");
        if (usesDeadList)
        {
            source.Append("    if (attributes.alive)\n")
                .Append("    {\n")
                .Append("        AnityVfxStoreAttributes(particleIndex, attributes);\n")
                .Append("    }\n")
                .Append("    else\n")
                .Append("    {\n")
                .Append("        VFXAttributes deadAttributes = AnityVfxLoadAttributes(particleIndex);\n")
                .Append("        deadAttributes.alive = false;\n")
                .Append("        AnityVfxStoreAttributes(particleIndex, deadAttributes);\n")
                .Append("        uint deadIndex;\n")
                .Append("        InterlockedAdd(deadListCount[0], 1u, deadIndex);\n")
                .Append("        if (deadIndex < deadListCapacity)\n")
                .Append("        {\n")
                .Append("            deadListOut[deadIndex] = particleIndex;\n")
                .Append("        }\n")
                .Append("        else\n")
                .Append("        {\n")
                .Append("            uint ignoredDeadCount;\n")
                .Append("            InterlockedAdd(deadListCount[0], 0xffffffffu, ignoredDeadCount);\n")
                .Append("        }\n")
                .Append("    }\n");
        }
        else
        {
            source.Append("    AnityVfxStoreAttributes(particleIndex, attributes);\n");
        }
        source.Append("}\n");
        return new VfxContextKernelCompilation(
            contextId,
            source.ToString(),
            blocks.Select(block => block.Model.FileId).ToArray(),
            usesRandom,
            usesSource,
            usesDeadList,
            false,
            false,
            storedAttributes,
            sourceAttributes,
            ThreadGroupSize);
    }

    /// <summary>
    /// Produces the stable runtime-facing geometry, render-state, and particle-layout contract for
    /// an official Planar Output whose complete shader program is not executable yet. This is kept
    /// separate from <see cref="Compile"/> so callers cannot accidentally execute a descriptor-only
    /// output as a generated shader.
    /// </summary>
    internal static VfxContextKernelCompilation DescribePlanarOutput(VfxTypedGraph graph, long contextId)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));
        VfxContextSchema schema = VfxContextSchema.Create(graph);
        if (!schema.ContextsById.TryGetValue(contextId, out VfxContextDescriptor? context))
            throw new KeyNotFoundException($"VFX Planar Output descriptor cannot resolve context '{contextId}'.");
        if (context.ContextType != VfxContextType.Output ||
            context.Model.ScriptType.TypeName != "VFXPlanarPrimitiveOutput")
            throw new NotSupportedException(
                $"VFX context '{contextId}' is not a Planar Output context.");
        if (context.Data?.DataType != VfxDataType.Particle)
            throw new NotSupportedException(
                $"VFX Planar Output context '{contextId}' requires non-strip particle data.");

        int verticesPerParticle = context.Task switch
        {
            VfxTaskKind.ParticleTriangleOutput => 3,
            VfxTaskKind.ParticleQuadOutput => 4,
            VfxTaskKind.ParticleOctagonOutput => 8,
            _ => throw new NotSupportedException(
                $"VFX Planar Output context '{contextId}' task '{context.Task}' is not implemented.")
        };
        int uvMode = VfxYamlFields.ReadInt32(context.Model.Document.RawText, "uvMode") ?? 0;
        if (uvMode is < 0 or > 4)
            throw new InvalidDataException(
                $"VFX Planar Output context '{contextId}' has invalid uvMode '{uvMode}'.");
        IReadOnlyDictionary<long, VfxSerializedAttributeUsage> usagesByModel =
            VfxAttributeUsageSet.Create(graph).Usages
                .Where(usage => usage.Model.ScriptType.TypeName is "SetAttribute" or "SetCustomAttribute")
                .ToDictionary(usage => usage.Model.FileId);
        VfxAttributeDefinition[] storedAttributes = CollectDataWideStoredAttributes(
            graph, schema, context.Data, usagesByModel);

        return new VfxContextKernelCompilation(
            contextId,
            string.Empty,
            Array.Empty<long>(),
            false,
            false,
            false,
            false,
            false,
            storedAttributes,
            Array.Empty<VfxAttributeDefinition>(),
            0,
            VfxKernelStage.Vertex,
            verticesPerParticle,
            true,
            null,
            null,
            PlanarIndexPattern(context.Task),
            ReadPlanarRenderState(context.Model),
            runtimeExecutable: false);
    }

    private static VfxContextKernelCompilation CompileOutputEvent(
        VfxTypedGraph graph,
        VfxContextSchema schema,
        VfxContextDescriptor context)
    {
        string eventName = context.EventName
            ?? throw new InvalidDataException($"VFX Output Event context '{context.Model.FileId}' requires an event name.");
        VfxFlowEdge[] currentInputs = graph.FlowEdges
            .Where(edge => edge.TargetContextId == context.Model.FileId)
            .ToArray();
        if (currentInputs.Length == 0)
            throw new InvalidOperationException(
                $"VFX Output Event context '{context.Model.FileId}' cannot be compiled without an input Spawner.");

        var graphContextOrder = graph.Contexts
            .Select((model, index) => (model.FileId, index))
            .ToDictionary(item => item.FileId, item => item.index);
        VfxContextDescriptor[] matchingContexts = schema.ContextsById.Values
            .Where(candidate =>
                candidate.ContextType == VfxContextType.OutputEvent &&
                string.Equals(candidate.EventName, eventName, StringComparison.Ordinal) &&
                graph.FlowEdges.Any(edge => edge.TargetContextId == candidate.Model.FileId))
            .OrderBy(candidate => graphContextOrder[candidate.Model.FileId])
            .ToArray();
        HashSet<long> matchingIds = matchingContexts
            .Select(candidate => candidate.Model.FileId)
            .ToHashSet();
        VfxFlowEdge[] directInputEdges = graph.FlowEdges
            .Where(edge => matchingIds.Contains(edge.TargetContextId))
            .ToArray();
        foreach (VfxFlowEdge edge in directInputEdges)
        {
            VfxContextDescriptor source = schema.ContextsById[edge.SourceContextId];
            if (source.ContextType != VfxContextType.Spawner)
                throw new InvalidOperationException(
                    $"VFX Output Event '{eventName}' has unexpected direct input context " +
                    $"'{source.Model.FileId}' of type '{source.ContextType}'; Unity requires a Spawner.");
        }

        long[] sourceSpawnerIds = directInputEdges
            .Select(edge => edge.SourceContextId)
            .Distinct()
            .ToArray();
        var mappings = sourceSpawnerIds
            .Select(sourceId => new VfxOutputEventBufferMapping("spawner_input", sourceId))
            .ToArray();

        HashSet<long> ancestors = CollectIncomingContextAncestors(graph, matchingIds);
        IReadOnlyDictionary<long, VfxSerializedAttributeUsage> usagesByModel =
            VfxAttributeUsageSet.Create(graph).Usages
                .Where(usage => usage.Model.ScriptType.TypeName == "VFXSpawnerSetAttribute")
                .ToDictionary(usage => usage.Model.FileId);
        VfxModel[] attributeBlocks = ancestors
            .SelectMany(parentId => graph.ModelsByFileId[parentId].ChildrenIds)
            .Select(childId => graph.ModelsByFileId[childId])
            .Where(child => child.ScriptType.TypeName == "VFXSpawnerSetAttribute")
            .GroupBy(child => child.FileId)
            .Select(group => group.First())
            .OrderBy(child => child.FileId)
            .ToArray();
        VfxAttributeDefinition[] eventAttributes = DistinctAttributes(
                attributeBlocks.SelectMany(block => usagesByModel[block.FileId].Attributes))
            .ToArray();

        return new VfxContextKernelCompilation(
            context.Model.FileId,
            string.Empty,
            attributeBlocks.Select(block => block.FileId).ToArray(),
            false,
            false,
            false,
            false,
            false,
            Array.Empty<VfxAttributeDefinition>(),
            Array.Empty<VfxAttributeDefinition>(),
            0,
            stage: VfxKernelStage.Cpu,
            outputEventName: eventName,
            outputEventContextIds: matchingContexts.Select(candidate => candidate.Model.FileId).ToArray(),
            outputEventBufferMappings: mappings,
            outputEventAttributes: eventAttributes,
            outputEventAttributeMode: VfxAttributeMode.ReadSource,
            disablesInstancing: true);
    }

    private static HashSet<long> CollectIncomingContextAncestors(
        VfxTypedGraph graph,
        IEnumerable<long> startContextIds)
    {
        var ancestors = new HashSet<long>();
        var pending = new Stack<long>(startContextIds.Reverse());
        while (pending.Count != 0)
        {
            long current = pending.Pop();
            if (!ancestors.Add(current)) continue;
            foreach (long parentId in graph.FlowEdges
                         .Where(edge => edge.TargetContextId == current)
                         .Select(edge => edge.SourceContextId)
                         .Reverse())
                pending.Push(parentId);
        }
        return ancestors;
    }

    private static VfxContextKernelCompilation CompilePlanarOutput(
        VfxTypedGraph graph,
        VfxContextSchema schema,
        VfxContextDescriptor context)
    {
        if (context.Data?.DataType != VfxDataType.Particle)
            throw new NotSupportedException(
                $"VFX Planar Output context '{context.Model.FileId}' requires non-strip particle data.");
        if (ReadBooleanSetting(context.Model, "useGeometryShader", false))
            throw new NotSupportedException(
                $"VFX Planar Output context '{context.Model.FileId}' geometry-shader expansion is not implemented.");
        if (VfxYamlFields.ReadReference(context.Model.Document.RawText, "shaderGraph") != 0)
            throw new NotSupportedException(
                $"VFX Planar Output context '{context.Model.FileId}' Shader Graph material passes are not implemented.");
        if (context.Model.ChildrenIds.Count != 0)
            throw new NotSupportedException(
                $"VFX Planar Output context '{context.Model.FileId}' contains output blocks that are not implemented.");

        int verticesPerParticle = context.Task switch
        {
            VfxTaskKind.ParticleTriangleOutput => 3,
            VfxTaskKind.ParticleQuadOutput => 4,
            VfxTaskKind.ParticleOctagonOutput => 8,
            _ => throw new NotSupportedException(
                $"VFX Planar Output context '{context.Model.FileId}' task '{context.Task}' is not implemented.")
        };
        IReadOnlyDictionary<long, VfxSerializedAttributeUsage> usagesByModel =
            VfxAttributeUsageSet.Create(graph).Usages
                .Where(usage => usage.Model.ScriptType.TypeName is "SetAttribute" or "SetCustomAttribute")
                .ToDictionary(usage => usage.Model.FileId);
        VfxAttributeDefinition[] storedAttributes = CollectDataWideStoredAttributes(
            graph, schema, context.Data, usagesByModel);
        int aliveOffset = AttributeOffset(storedAttributes, "alive");
        int uvMode = VfxYamlFields.ReadInt32(context.Model.Document.RawText, "uvMode") ?? 0;
        bool usesFlipbook = uvMode is 1 or 2 or 4;
        VfxPlanarRenderState renderState = ReadPlanarRenderState(context.Model);
        if ((VfxYamlFields.ReadInt32(context.Model.Document.RawText, "colorMapping") ?? 0) != 0)
            throw new NotSupportedException(
                $"VFX Planar Output context '{context.Model.FileId}' Gradient Mapped color requires gradient texture generation.");
        if (ReadBooleanSetting(context.Model, "useSoftParticle", false))
            throw new NotSupportedException(
                $"VFX Planar Output context '{context.Model.FileId}' soft particles require URP depth texture sampling.");
        int flipbookLayout = VfxYamlFields.ReadInt32(context.Model.Document.RawText, "flipbookLayout") ?? 0;
        if (flipbookLayout is not (0 or 1))
            throw new InvalidDataException(
                $"VFX Planar Output context '{context.Model.FileId}' has invalid flipbookLayout '{flipbookLayout}'.");
        if (usesFlipbook && flipbookLayout != 0)
            throw new NotSupportedException(
                $"VFX Planar Output context '{context.Model.FileId}' Texture2DArray flipbooks are not implemented.");
        string cropFactorDeclaration = "    float cropFactor = 0.293;\n";
        if (context.Task == VfxTaskKind.ParticleOctagonOutput)
            cropFactorDeclaration = CompileOutputFloatInput(graph, context.Model, "cropFactor", 0.293);
        string uvParameterDeclarations = CompilePlanarUvParameterDeclarations(graph, context.Model, uvMode);
        string alphaThresholdDeclaration = renderState.AlphaClipping
            ? CompileOutputFloatInput(graph, context.Model, "alphaThreshold", 0.5)
            : string.Empty;
        IReadOnlyList<int> indexPattern = PlanarIndexPattern(context.Task);

        var source = new StringBuilder();
        source.Append(VfxAttributeCodeGenerator.GenerateAttributeStructFromAttributes(storedAttributes)).Append('\n')
            .Append("ByteAddressBuffer attributeBuffer : register(t0);\n");
        AppendReadOnlyAttributeBufferHelpers(source, storedAttributes, aliveOffset);
        source.Append(PlanarTransformHelpers)
            .Append("\nstruct AnityVfxPlanarVertexOutput\n")
            .Append("{\n")
            .Append("    float4 positionCS : SV_POSITION;\n")
            .Append("    float3 positionWS : TEXCOORD0;\n")
            .Append(uvMode is 2 or 4 ? "    float4 uv : TEXCOORD1;\n" : "    float2 uv : TEXCOORD1;\n")
            .Append("    float4 color : COLOR0;\n")
            .Append("    nointerpolation uint particleIndex : TEXCOORD2;\n")
            .Append(uvMode is 2 or 4 ? "    nointerpolation float frameBlend : TEXCOORD3;\n" : string.Empty)
            .Append(uvMode == 4 ? "    nointerpolation float2 motionVectorScale : TEXCOORD4;\n" : string.Empty)
            .Append(renderState.AlphaClipping ? "    nointerpolation float alphaThreshold : TEXCOORD5;\n" : string.Empty)
            .Append("};\n\n")
            .Append("cbuffer AnityVfxPlanarDispatch : register(b0)\n")
            .Append("{\n")
            .Append("    uint particleCount;\n")
            .Append("    float4x4 vfxToWorld;\n")
            .Append("    float4x4 worldToClip;\n")
            .Append("};\n\n")
            .Append("AnityVfxPlanarVertexOutput AnityVfxCullPlanarVertex(uint particleIndex)\n")
            .Append("{\n")
            .Append("    AnityVfxPlanarVertexOutput output = (AnityVfxPlanarVertexOutput)0;\n")
            .Append("    float nanValue = asfloat(0x7fc00000u);\n")
            .Append("    output.positionCS = float4(nanValue, nanValue, nanValue, nanValue);\n")
            .Append("    output.particleIndex = particleIndex;\n")
            .Append("    return output;\n")
            .Append("}\n\n")
            .Append("#pragma vertex AnityVfxPlanarVert\n")
            .Append("AnityVfxPlanarVertexOutput AnityVfxPlanarVert(uint vertexId : SV_VertexID)\n")
            .Append("{\n")
            .Append("    const uint verticesPerParticle = ").Append(verticesPerParticle).Append("u;\n")
            .Append("    uint particleIndex = vertexId / verticesPerParticle;\n")
            .Append("    uint localVertexId = vertexId % verticesPerParticle;\n")
            .Append("    if (particleIndex >= particleCount) return AnityVfxCullPlanarVertex(particleIndex);\n")
            .Append("    if (!AnityVfxLoadAlive(particleIndex)) return AnityVfxCullPlanarVertex(particleIndex);\n")
            .Append("    VFXAttributes attributes = AnityVfxLoadAttributes(particleIndex);\n")
            .Append("    float2 vOffsets;\n")
            .Append("    float2 uv;\n");
        AppendPlanarVertexOffsets(source, context.Task, cropFactorDeclaration);
        source.Append(uvParameterDeclarations);
        AppendPlanarUvTransform(source, uvMode);
        source.Append(alphaThresholdDeclaration);
        source.Append("    float3 size3 = float3(attributes.size, attributes.size, attributes.size);\n")
            .Append("    size3.x *= attributes.scaleX;\n")
            .Append("    size3.y *= attributes.scaleY;\n")
            .Append("    size3.z *= attributes.scaleZ;\n")
            .Append("    float4x4 elementToVFX = AnityVfxGetElementToVFXMatrix(\n")
            .Append("        attributes.axisX, attributes.axisY, attributes.axisZ,\n")
            .Append("        float3(attributes.angleX, attributes.angleY, attributes.angleZ),\n")
            .Append("        float3(attributes.pivotX, attributes.pivotY, attributes.pivotZ),\n")
            .Append("        size3, attributes.position);\n")
            .Append("    float3 positionVFX = mul(elementToVFX, float4(vOffsets, 0.0, 1.0)).xyz;\n")
            .Append("    float3 positionWS = mul(vfxToWorld, float4(positionVFX, 1.0)).xyz;\n")
            .Append("    AnityVfxPlanarVertexOutput output = (AnityVfxPlanarVertexOutput)0;\n")
            .Append("    output.positionCS = mul(worldToClip, float4(positionWS, 1.0));\n")
            .Append("    output.positionWS = positionWS;\n")
            .Append(uvMode is 2 or 4 ? "    output.uv = flipbookUv;\n" : "    output.uv = uv;\n")
            .Append("    output.color = float4(attributes.color, attributes.alpha);\n")
            .Append("    output.particleIndex = particleIndex;\n")
            .Append(uvMode is 2 or 4 ? "    output.frameBlend = frameBlend;\n" : string.Empty)
            .Append(uvMode == 4 ? "    output.motionVectorScale = motionVectorScale * invFlipBookSize;\n" : string.Empty)
            .Append(renderState.AlphaClipping ? "    output.alphaThreshold = alphaThreshold;\n" : string.Empty)
            .Append("    return output;\n")
            .Append("}\n\n");
        AppendPlanarFragmentShader(source, uvMode, renderState);

        return new VfxContextKernelCompilation(
            context.Model.FileId,
            source.ToString(),
            Array.Empty<long>(),
            false,
            false,
            false,
            false,
            false,
            storedAttributes,
            Array.Empty<VfxAttributeDefinition>(),
            0,
            VfxKernelStage.Vertex,
            verticesPerParticle,
            true,
            "AnityVfxPlanarVert",
            "AnityVfxPlanarFrag",
            indexPattern,
            renderState);
    }

    private static VfxPlanarRenderState ReadPlanarRenderState(VfxModel output)
    {
        int blendValue = VfxYamlFields.ReadInt32(output.Document.RawText, "blendMode") ?? 1;
        if (blendValue is < 0 or > 3)
            throw new InvalidDataException($"VFX Planar Output '{output.FileId}' has invalid blendMode '{blendValue}'.");
        int cullValue = VfxYamlFields.ReadInt32(output.Document.RawText, "cullMode") ?? 0;
        if (cullValue is < 0 or > 3)
            throw new InvalidDataException($"VFX Planar Output '{output.FileId}' has invalid cullMode '{cullValue}'.");
        int zWriteValue = VfxYamlFields.ReadInt32(output.Document.RawText, "zWriteMode") ?? 0;
        if (zWriteValue is < 0 or > 2)
            throw new InvalidDataException($"VFX Planar Output '{output.FileId}' has invalid zWriteMode '{zWriteValue}'.");
        int zTestValue = VfxYamlFields.ReadInt32(output.Document.RawText, "zTestMode") ?? 0;
        if (zTestValue is < 0 or > 7)
            throw new InvalidDataException($"VFX Planar Output '{output.FileId}' has invalid zTestMode '{zTestValue}'.");
        int sortValue = VfxYamlFields.ReadInt32(output.Document.RawText, "sort") ?? 0;
        if (sortValue is < 0 or > 2)
            throw new InvalidDataException($"VFX Planar Output '{output.FileId}' has invalid sort '{sortValue}'.");

        VfxPlanarBlendMode blendMode = (VfxPlanarBlendMode)blendValue;
        bool opaque = blendMode == VfxPlanarBlendMode.Opaque;
        bool alphaClipping = ReadBooleanSetting(output, "useAlphaClipping", false);
        int sortingPriority = VfxYamlFields.ReadInt32(output.Document.RawText, "sortingPriority") ?? 0;
        sortingPriority = Math.Clamp(sortingPriority, -50, 50);
        string queue = opaque
            ? (alphaClipping ? "AlphaTest" : "Geometry")
            : "Transparent";
        if (sortingPriority != 0)
            queue += sortingPriority.ToString("+0;-0", CultureInfo.InvariantCulture);
        return new VfxPlanarRenderState(
            blendMode,
            cullValue switch
            {
                0 or 3 => VfxPlanarCullMode.Off,
                1 => VfxPlanarCullMode.Front,
                2 => VfxPlanarCullMode.Back,
                _ => throw new InvalidOperationException()
            },
            zWriteValue switch
            {
                0 => opaque,
                1 => false,
                2 => true,
                _ => throw new InvalidOperationException()
            },
            zTestValue switch
            {
                0 or 3 => VfxPlanarZTest.LEqual,
                1 => VfxPlanarZTest.Less,
                2 => VfxPlanarZTest.Greater,
                4 => VfxPlanarZTest.GEqual,
                5 => VfxPlanarZTest.Equal,
                6 => VfxPlanarZTest.NotEqual,
                7 => VfxPlanarZTest.Always,
                _ => throw new InvalidOperationException()
            },
            alphaClipping,
            queue,
            sortValue == 2 ||
            (sortValue == 0 && blendMode is VfxPlanarBlendMode.Alpha or VfxPlanarBlendMode.AlphaPremultiplied),
            ReadBooleanSetting(output, "indirectDraw", false));
    }

    private static IReadOnlyList<int> PlanarIndexPattern(VfxTaskKind task)
        => task switch
        {
            VfxTaskKind.ParticleTriangleOutput => new[] { 0, 1, 2 },
            VfxTaskKind.ParticleQuadOutput => new[] { 0, 2, 1, 1, 2, 3 },
            VfxTaskKind.ParticleOctagonOutput => new[]
            {
                0, 1, 2, 0, 2, 3, 0, 3, 4,
                0, 4, 5, 0, 5, 6, 0, 6, 7
            },
            _ => throw new NotSupportedException($"VFX Planar Output task '{task}' has no index pattern.")
        };

    private static string CompileOutputFloatInput(
        VfxTypedGraph graph,
        VfxModel output,
        string name,
        double defaultValue)
    {
        VfxModel? slot = output.InputSlotIds
            .Select(id => graph.ModelsByFileId[id])
            .FirstOrDefault(candidate => string.Equals(candidate.SlotProperty?.Name, name, StringComparison.Ordinal));
        if (slot is null)
            return "    float " + name + " = " + HlslLiteral(defaultValue) + ";\n";
        if (slot.SlotProperty?.Value.Kind != VfxSlotValueKind.Float)
            throw new InvalidDataException(
                $"VFX Planar Output '{output.FileId}' input '{name}' must be Float.");
        if (slot.LinkedSlotIds.Count == 0)
            return "    " + HlslDeclaration(name, slot.SlotProperty.Value) + "\n";
        VfxExpressionCompilation expression = VfxExpressionCompiler.CompileInput(graph, slot.FileId);
        if (expression.ResultType != VfxExpressionValueType.Float)
            throw new InvalidDataException(
                $"VFX Planar Output '{output.FileId}' input '{name}' expression must produce Float.");
        if (expression.AttributeDependencies.Any(dependency => dependency.Mode != VfxAttributeMode.Read))
            throw new NotSupportedException(
                $"VFX Planar Output '{output.FileId}' input '{name}' cannot read source attributes.");
        var declaration = new StringBuilder();
        foreach (string line in expression.HlslSource.Split('\n'))
        {
            if (line.Length != 0) declaration.Append("    ").Append(line).Append('\n');
        }
        declaration.Append("    float ").Append(name).Append(" = ")
            .Append(expression.ResultVariable).Append(";\n");
        return declaration.ToString();
    }

    private static string CompileOutputFloat2Input(
        VfxTypedGraph graph,
        VfxModel output,
        string name,
        double defaultX,
        double defaultY)
    {
        VfxModel? slot = output.InputSlotIds
            .Select(id => graph.ModelsByFileId[id])
            .FirstOrDefault(candidate => string.Equals(candidate.SlotProperty?.Name, name, StringComparison.Ordinal));
        if (slot is null)
            return "    float2 " + name + " = float2(" + HlslLiteral(defaultX) + ", " +
                   HlslLiteral(defaultY) + ");\n";
        if (slot.SlotProperty?.Value.Kind != VfxSlotValueKind.Float2)
            throw new InvalidDataException(
                $"VFX Planar Output '{output.FileId}' input '{name}' must be Float2.");
        if (slot.LinkedSlotIds.Count == 0)
            return "    " + HlslDeclaration(name, slot.SlotProperty.Value) + "\n";
        VfxExpressionCompilation expression = VfxExpressionCompiler.CompileInput(graph, slot.FileId);
        if (expression.ResultType != VfxExpressionValueType.Float2)
            throw new InvalidDataException(
                $"VFX Planar Output '{output.FileId}' input '{name}' expression must produce Float2.");
        if (expression.AttributeDependencies.Any(dependency => dependency.Mode != VfxAttributeMode.Read))
            throw new NotSupportedException(
                $"VFX Planar Output '{output.FileId}' input '{name}' cannot read source attributes.");
        var declaration = new StringBuilder();
        foreach (string line in expression.HlslSource.Split('\n'))
        {
            if (line.Length != 0) declaration.Append("    ").Append(line).Append('\n');
        }
        declaration.Append("    float2 ").Append(name).Append(" = ")
            .Append(expression.ResultVariable).Append(";\n");
        return declaration.ToString();
    }

    private static string CompilePlanarUvParameterDeclarations(
        VfxTypedGraph graph,
        VfxModel output,
        int uvMode)
        => uvMode switch
        {
            0 => string.Empty,
            1 or 2 => CompileOutputFloat2Input(graph, output, "flipBookSize", 4.0, 4.0),
            3 => CompileOutputFloat2Input(graph, output, "uvScale", 1.0, 1.0) +
                 CompileOutputFloat2Input(graph, output, "uvBias", 0.0, 0.0),
            4 => CompileOutputFloat2Input(graph, output, "flipBookSize", 4.0, 4.0) +
                 CompileOutputFloatInput(graph, output, "motionVectorScale", 1.0),
            _ => throw new InvalidDataException($"VFX Planar Output has invalid uvMode '{uvMode}'.")
        };

    private static void AppendPlanarUvTransform(StringBuilder source, int uvMode)
    {
        switch (uvMode)
        {
            case 0:
                break;
            case 1:
                source.Append("    float2 invFlipBookSize = 1.0 / flipBookSize;\n")
                    .Append("    float frameIndex = attributes.texIndex - frac(attributes.texIndex);\n")
                    .Append("    uv = AnityVfxGetSubUV(frameIndex, uv, flipBookSize, invFlipBookSize);\n");
                break;
            case 2:
            case 4:
                source.Append("    float2 invFlipBookSize = 1.0 / flipBookSize;\n")
                    .Append("    float frameBlend = frac(attributes.texIndex);\n")
                    .Append("    float frameIndex = attributes.texIndex - frameBlend;\n")
                    .Append("    float4 flipbookUv = float4(\n")
                    .Append("        AnityVfxGetSubUV(frameIndex, uv, flipBookSize, invFlipBookSize),\n")
                    .Append("        AnityVfxGetSubUV(frameIndex + 1.0, uv, flipBookSize, invFlipBookSize));\n");
                break;
            case 3:
                source.Append("    uv = uv * uvScale + uvBias;\n");
                break;
            default:
                throw new InvalidDataException($"VFX Planar Output has invalid uvMode '{uvMode}'.");
        }
    }

    private static void AppendPlanarFragmentShader(
        StringBuilder source,
        int uvMode,
        VfxPlanarRenderState renderState)
    {
        source.Append("Texture2D<float4> mainTexture : register(t1);\n")
            .Append("SamplerState mainTextureSampler : register(s0);\n");
        if (uvMode == 4)
            source.Append("Texture2D<float4> motionVectorMap : register(t2);\n")
                .Append("SamplerState motionVectorSampler : register(s1);\n");
        source.Append("\n#pragma fragment AnityVfxPlanarFrag\n")
            .Append("float4 AnityVfxPlanarFrag(AnityVfxPlanarVertexOutput input) : SV_Target0\n")
            .Append("{\n")
            .Append("    float4 textureColor;\n");
        switch (uvMode)
        {
            case 0:
            case 1:
            case 3:
                source.Append("    textureColor = mainTexture.Sample(mainTextureSampler, input.uv);\n");
                break;
            case 2:
                source.Append("    float4 frame0 = mainTexture.Sample(mainTextureSampler, input.uv.xy);\n")
                    .Append("    float4 frame1 = mainTexture.Sample(mainTextureSampler, input.uv.zw);\n")
                    .Append("    textureColor = lerp(frame0, frame1, input.frameBlend);\n");
                break;
            case 4:
                source.Append("    float2 motion0 = motionVectorMap.Sample(motionVectorSampler, input.uv.xy).rg * 2.0 - 1.0;\n")
                    .Append("    float2 motion1 = motionVectorMap.Sample(motionVectorSampler, input.uv.zw).rg * 2.0 - 1.0;\n")
                    .Append("    float2 offset0 = motion0 * (-input.motionVectorScale * input.frameBlend);\n")
                    .Append("    float2 offset1 = motion1 * (input.motionVectorScale * (1.0 - input.frameBlend));\n")
                    .Append("    float4 frame0 = mainTexture.Sample(mainTextureSampler, input.uv.xy + offset0);\n")
                    .Append("    float4 frame1 = mainTexture.Sample(mainTextureSampler, input.uv.zw + offset1);\n")
                    .Append("    textureColor = lerp(frame0, frame1, input.frameBlend);\n");
                break;
            default:
                throw new InvalidDataException($"VFX Planar Output has invalid uvMode '{uvMode}'.");
        }
        source.Append("    float4 outputColor = input.color * textureColor;\n");
        if (renderState.AlphaClipping)
            source.Append("    clip(outputColor.a - input.alphaThreshold);\n");
        source.Append("    outputColor.a = saturate(outputColor.a);\n")
            .Append("    return outputColor;\n")
            .Append("}\n");
    }

    private static void AppendPlanarVertexOffsets(
        StringBuilder source,
        VfxTaskKind task,
        string cropFactorDeclaration)
    {
        switch (task)
        {
            case VfxTaskKind.ParticleTriangleOutput:
                source.Append("    static const float2 offsets[3] = {\n")
                    .Append("        float2(-0.5, -0.288675129413604736328125),\n")
                    .Append("        float2(0.0, 0.57735025882720947265625),\n")
                    .Append("        float2(0.5, -0.288675129413604736328125) };\n")
                    .Append("    vOffsets = offsets[localVertexId];\n")
                    .Append("    uv = (vOffsets * 0.866025388240814208984375) + 0.5;\n");
                break;
            case VfxTaskKind.ParticleQuadOutput:
                source.Append("    uv = float2(float(localVertexId & 1u), float(localVertexId & 2u) * 0.5);\n")
                    .Append("    vOffsets = uv - 0.5;\n");
                break;
            case VfxTaskKind.ParticleOctagonOutput:
                source.Append(cropFactorDeclaration)
                    .Append("    static const float2 offsets[8] = {\n")
                    .Append("        float2(-0.5, 0.0), float2(-0.5, 0.5),\n")
                    .Append("        float2(0.0, 0.5), float2(0.5, 0.5),\n")
                    .Append("        float2(0.5, 0.0), float2(0.5, -0.5),\n")
                    .Append("        float2(0.0, -0.5), float2(-0.5, -0.5) };\n")
                    .Append("    float correctedCropFactor = (localVertexId & 1u) != 0u ? 1.0 - cropFactor : 1.0;\n")
                    .Append("    vOffsets = offsets[localVertexId] * correctedCropFactor;\n")
                    .Append("    uv = vOffsets + 0.5;\n");
                break;
            default:
                throw new NotSupportedException($"VFX Planar Output task '{task}' is not implemented.");
        }
    }

    private static VfxContextKernelCompilation CompileInitialize(
        VfxTypedGraph graph,
        VfxContextSchema schema,
        VfxContextDescriptor context)
    {
        bool usesGpuEventSource = HasGpuEventSource(graph, context.Model.FileId);
        IReadOnlyDictionary<long, VfxSerializedAttributeUsage> usagesByModel =
            VfxAttributeUsageSet.Create(graph).Usages
                .Where(usage => usage.Model.ScriptType.TypeName is "SetAttribute" or "SetCustomAttribute")
                .ToDictionary(usage => usage.Model.FileId);
        var blocks = new List<CompiledBlock>();
        foreach (long childId in context.Model.ChildrenIds)
        {
            VfxModel child = graph.ModelsByFileId[childId];
            ActivationCompilation activation = CompileActivation(graph, child, ReadDisabled(child));
            if (!activation.IsIncluded) continue;
            if (child.Kind != VfxModelKind.Block ||
                child.ScriptType.TypeName is not ("SetAttribute" or "SetCustomAttribute"))
                throw new NotSupportedException(
                    $"VFX Initialize kernel does not support enabled child '{child.FileId}' type '{child.ScriptType.TypeName}'.");
            blocks.Add(CompileBlock(graph, child, usagesByModel, activation));
        }
        if (blocks.Count == 0 && !usesGpuEventSource)
            throw new NotSupportedException(
                $"VFX Initialize context '{context.Model.FileId}' has no enabled supported blocks to compile.");

        VfxSerializedAttributeUsage[] usages = blocks.Select(block => block.Usage!).ToArray();
        VfxExpressionAttributeDependency[] dependencies = blocks
            .SelectMany(block => block.AttributeDependencies)
            .GroupBy(dependency => (dependency.Attribute.Name, dependency.Mode))
            .Select(group => group.First())
            .ToArray();
        bool usesRandom = usages.Any(usage => usage.RequiresRandomSeed);
        VfxAttributeDefinition[] currentAttributes = blocks.SelectMany(block => block.AttributeAccesses)
            .Select(access => access.Attribute)
            .Concat(dependencies.Where(dependency => dependency.Mode == VfxAttributeMode.Read)
                .Select(dependency => dependency.Attribute))
            .Concat(usesRandom ? new[] { VfxAttributeCatalog.Find("seed") } : Array.Empty<VfxAttributeDefinition>())
            .Concat(usesGpuEventSource
                ? new[] { VfxAttributeCatalog.Find("alive") }
                : Array.Empty<VfxAttributeDefinition>())
            .ToArray();
        VfxAttributeDefinition[] sourceAttributes = usages
            .Where(usage => usage.ValueSource == VfxAttributeValueSource.Source)
            .SelectMany(usage => usage.Attributes)
            .Concat(dependencies.Where(dependency => dependency.Mode == VfxAttributeMode.ReadSource)
                .Select(dependency => dependency.Attribute))
            .ToArray();
        sourceAttributes = DistinctAttributes(sourceAttributes).ToArray();
        VfxAttributeDefinition[] storedAttributes = CollectDataWideStoredAttributes(
            graph, schema, context.Data!, usagesByModel);
        bool usesSource = sourceAttributes.Length != 0;
        bool usesAlive = storedAttributes.Any(attribute => attribute.Name == "alive");
        bool usesDeadList = usesAlive && context.Data?.DataType == VfxDataType.Particle;
        var source = new StringBuilder();
        source.Append(VfxAttributeCodeGenerator.GenerateAttributeStructFromAttributes(storedAttributes)).Append('\n');
        if (usesSource)
            source.Append(VfxAttributeCodeGenerator.GenerateAttributeStructFromAttributes(
                sourceAttributes, "VFXSourceAttributes")).Append('\n');
        source.Append("RWByteAddressBuffer attributeBuffer : register(u0);\n");
        AppendAttributeBufferHelpers(source, storedAttributes);
        if (usesDeadList)
            source.Append("RWStructuredBuffer<uint> deadListIn : register(u1);\n")
                .Append("RWStructuredBuffer<uint> deadListCount : register(u2);\n");
        if (usesSource)
        {
            source.Append("ByteAddressBuffer sourceAttributeBuffer : register(t0);\n");
            AppendSourceAttributeBufferHelper(source, sourceAttributes);
        }
        if (usesGpuEventSource)
            source.Append("StructuredBuffer<uint> eventList : register(t1);\n");
        else if (usesSource)
            source.Append("StructuredBuffer<uint> spawnCountPrefixSum : register(t1);\n");
        source.Append("cbuffer AnityVfxInitializeDispatch : register(b0)\n{\n")
            .Append("    uint spawnCount;\n")
            .Append("    uint currentSpawnIndex;\n")
            .Append("    uint systemSeed;\n")
            .Append(usesSource ? "    uint startEventIndex;\n" : string.Empty)
            .Append(usesSource && !usesGpuEventSource ? "    uint sourceEventCount;\n" : string.Empty)
            .Append(usesDeadList ? "    uint deadListCountSnapshot;\n" : string.Empty)
            .Append("    float4x4 localToWorld;\n")
            .Append("    float4x4 worldToLocal;\n")
            .Append("};\n\n");
        if (usesRandom || storedAttributes.Any(attribute => attribute.Name == "seed"))
            source.Append(RandomHelpers).Append('\n');
        if (usesSource && !usesGpuEventSource)
            source.Append(PrefixSumBinarySearchHelper).Append('\n');

        source.Append("[numthreads(").Append(ThreadGroupSize).Append(", 1, 1)]\n")
            .Append("void Initialize(uint3 dispatchThreadId : SV_DispatchThreadID)\n{\n")
            .Append("    uint spawnThreadIndex = dispatchThreadId.x;\n")
            .Append("    uint maxSpawnCount = spawnCount;\n");
        if (usesDeadList)
            source.Append("    maxSpawnCount = min(maxSpawnCount, deadListCountSnapshot);\n");
        source.Append("    if (spawnThreadIndex >= maxSpawnCount) return;\n")
            .Append("    uint particleIndex = spawnThreadIndex + currentSpawnIndex;\n")
            .Append("    VFXAttributes attributes = (VFXAttributes)0;\n");
        foreach (VfxAttributeDefinition attribute in DistinctAttributes(storedAttributes))
            source.Append("    attributes.").Append(attribute.Name).Append(" = ")
                .Append(attribute.DefaultHlsl).Append(";\n");
        if (usesSource)
        {
            source.Append(usesGpuEventSource
                ? "    uint sourceIndex = eventList[spawnThreadIndex];\n"
                : "    uint sourceIndex = AnityVfxFindSourceIndex(spawnThreadIndex, sourceEventCount);\n")
                .Append("    VFXSourceAttributes sourceAttributes = AnityVfxLoadSourceAttributes(sourceIndex);\n");
        }
        if (storedAttributes.Any(attribute => attribute.Name == "particleId"))
            source.Append("    attributes.particleId = particleIndex;\n");
        if (storedAttributes.Any(attribute => attribute.Name == "seed"))
            source.Append("    attributes.seed = AnityVfxHash(particleIndex ^ systemSeed);\n");
        if (storedAttributes.Any(attribute => attribute.Name == "spawnIndex"))
            source.Append("    attributes.spawnIndex = spawnThreadIndex;\n");
        if (usesRandom)
            source.Append("    uint randomState = attributes.seed;\n");
        if (usesGpuEventSource)
            source.Append("    attributes.alive = true;\n");

        AppendBlocks(source, blocks, "    ");
        if (usesRandom) source.Append("    attributes.seed = randomState;\n");
        if (usesAlive)
            source.Append("    if (!attributes.alive) return;\n");
        if (usesDeadList)
        {
            source.Append("    uint previousDeadCount;\n")
                .Append("    InterlockedAdd(deadListCount[0], 0xffffffffu, previousDeadCount);\n")
                .Append("    uint outputParticleIndex = deadListIn[previousDeadCount - 1u];\n")
                .Append("    AnityVfxStoreAttributes(outputParticleIndex, attributes);\n");
        }
        else
        {
            source.Append("    AnityVfxStoreAttributes(particleIndex, attributes);\n");
        }
        source.Append("}\n");
        return new VfxContextKernelCompilation(
            context.Model.FileId,
            source.ToString(),
            blocks.Select(block => block.Model.FileId).ToArray(),
            usesRandom,
            usesSource,
            usesDeadList,
            usesGpuEventSource,
            usesSource,
            storedAttributes,
            sourceAttributes,
            ThreadGroupSize);
    }

    private static VfxAttributeDefinition[] CollectDataWideStoredAttributes(
        VfxTypedGraph graph,
        VfxContextSchema schema,
        VfxDataDescriptor data,
        IReadOnlyDictionary<long, VfxSerializedAttributeUsage> usagesByModel)
    {
        var attributes = new List<VfxAttributeDefinition>();
        var updateContexts = new List<(AttributeAccess[] Accesses, UpdateSettings Settings)>();
        foreach (VfxContextDescriptor owner in schema.Contexts.Where(candidate =>
                     candidate.Data?.Model.FileId == data.Model.FileId &&
                     candidate.ContextType is VfxContextType.Init or VfxContextType.Update or VfxContextType.Output))
        {
            if (owner.ContextType == VfxContextType.Output)
            {
                attributes.AddRange(CollectOutputStoredAttributes(owner.Model));
                continue;
            }
            IReadOnlyList<CompiledBlock> blocks = CompileLayoutBlocks(graph, owner, usagesByModel);
            VfxExpressionAttributeDependency[] dependencies = blocks
                .SelectMany(block => block.AttributeDependencies)
                .GroupBy(dependency => (dependency.Attribute.Name, dependency.Mode))
                .Select(group => group.First())
                .ToArray();
            AttributeAccess[] explicitAccesses = blocks.SelectMany(block => block.AttributeAccesses)
                .Concat(dependencies
                    .Where(dependency => dependency.Mode == VfxAttributeMode.Read)
                    .Select(dependency => new AttributeAccess(dependency.Attribute, VfxAttributeMode.Read)))
                .ToArray();
            attributes.AddRange(explicitAccesses.Select(access => access.Attribute));
            if (blocks.Any(block => block.Usage?.RequiresRandomSeed == true))
                attributes.Add(VfxAttributeCatalog.Find("seed"));

            if (owner.ContextType == VfxContextType.Update)
            {
                updateContexts.Add((explicitAccesses, ReadUpdateSettings(owner.Model)));
            }
            else if (HasGpuEventSource(graph, owner.Model.FileId))
            {
                attributes.Add(VfxAttributeCatalog.Find("alive"));
            }
        }

        foreach ((AttributeAccess[] accesses, UpdateSettings settings) in updateContexts)
        {
            ImplicitUpdateCompilation implicitUpdate = CompileImplicitUpdate(
                accesses,
                DistinctAttributes(attributes).ToArray(),
                settings);
            attributes.AddRange(implicitUpdate.AttributeAccesses.Select(access => access.Attribute));
        }

        VfxAttributeDefinition? localOnly = attributes.FirstOrDefault(attribute => attribute.IsLocalOnly);
        if (localOnly is not null)
            throw new NotSupportedException(
                $"VFX particle data '{data.Model.FileId}' local-only attribute '{localOnly.Name}' requires event/strip-local storage and cannot enter the particle buffer ABI.");
        foreach (IGrouping<string, VfxAttributeDefinition> group in attributes.GroupBy(
                     attribute => attribute.Name,
                     StringComparer.Ordinal))
        {
            VfxAttributeDefinition first = group.First();
            if (group.Any(attribute =>
                    attribute.ValueType != first.ValueType ||
                    attribute.Space != first.Space ||
                    attribute.ComponentCount != first.ComponentCount))
                throw new InvalidDataException(
                    $"VFX particle data '{data.Model.FileId}' attribute '{group.Key}' has conflicting type or space definitions.");
        }
        VfxAttributeDefinition[] stored = DistinctAttributes(attributes).ToArray();
        if (stored.Length == 0)
            throw new NotSupportedException(
                $"VFX particle data '{data.Model.FileId}' has no supported stored attributes to compile.");
        return stored;
    }

    private static IReadOnlyList<VfxAttributeDefinition> CollectOutputStoredAttributes(VfxModel output)
    {
        string[] names = output.ScriptType.TypeName switch
        {
            "VFXPlanarPrimitiveOutput" => new[]
            {
                "position", "color", "alpha", "alive",
                "axisX", "axisY", "axisZ",
                "angleX", "angleY", "angleZ",
                "pivotX", "pivotY", "pivotZ",
                "size", "scaleX", "scaleY", "scaleZ"
            },
            "VFXQuadStripOutput" => new[]
            {
                "position", "color", "alpha",
                "axisX", "axisY", "axisZ",
                "angleX", "angleY", "angleZ",
                "pivotX", "pivotY", "pivotZ", "size"
            },
            _ => throw new NotSupportedException(
                $"VFX output '{output.FileId}' type '{output.ScriptType.TypeName}' has no stored attribute profile.")
        };
        var attributes = names.Select(VfxAttributeCatalog.Find).ToList();
        int uvMode = VfxYamlFields.ReadInt32(output.Document.RawText, "uvMode") ?? 0;
        if (uvMode is < 0 or > 4)
            throw new InvalidDataException($"VFX output '{output.FileId}' has invalid uvMode '{uvMode}'.");
        bool supportsUv = output.ScriptType.TypeName == "VFXQuadStripOutput" ||
                          VfxYamlFields.ReadReference(output.Document.RawText, "shaderGraph") == 0;
        if (supportsUv && uvMode is 1 or 2 or 4)
            attributes.Add(VfxAttributeCatalog.Find("texIndex"));
        return attributes;
    }

    private static void AppendAttributeBufferHelpers(
        StringBuilder source,
        IReadOnlyList<VfxAttributeDefinition> storedAttributes)
    {
        int stride = checked(storedAttributes.Sum(attribute => attribute.ComponentCount * sizeof(uint)));
        source.Append("static const uint ANITY_VFX_ATTRIBUTE_STRIDE = ")
            .Append(stride.ToString(CultureInfo.InvariantCulture)).Append("u;\n")
            .Append("VFXAttributes AnityVfxLoadAttributes(uint particleIndex)\n")
            .Append("{\n")
            .Append("    uint baseAddress = particleIndex * ANITY_VFX_ATTRIBUTE_STRIDE;\n")
            .Append("    VFXAttributes attributes = (VFXAttributes)0;\n");
        int offset = 0;
        foreach (VfxAttributeDefinition attribute in storedAttributes)
        {
            source.Append("    attributes.").Append(attribute.Name).Append(" = ")
                .Append(AttributeLoadExpression(attribute, offset, "attributeBuffer")).Append(";\n");
            offset = checked(offset + attribute.ComponentCount * sizeof(uint));
        }
        source.Append("    return attributes;\n")
            .Append("}\n")
            .Append("void AnityVfxStoreAttributes(uint particleIndex, VFXAttributes attributes)\n")
            .Append("{\n")
            .Append("    uint baseAddress = particleIndex * ANITY_VFX_ATTRIBUTE_STRIDE;\n");
        offset = 0;
        foreach (VfxAttributeDefinition attribute in storedAttributes)
        {
            source.Append("    attributeBuffer.Store")
                .Append(attribute.ComponentCount == 1
                    ? string.Empty
                    : attribute.ComponentCount.ToString(CultureInfo.InvariantCulture))
                .Append("(baseAddress + ").Append(offset.ToString(CultureInfo.InvariantCulture)).Append("u, ")
                .Append(AttributeStoreExpression(attribute)).Append(");\n");
            offset = checked(offset + attribute.ComponentCount * sizeof(uint));
        }
        source.Append("}\n");
    }

    private static int AttributeOffset(
        IReadOnlyList<VfxAttributeDefinition> storedAttributes,
        string attributeName)
    {
        int offset = 0;
        foreach (VfxAttributeDefinition attribute in storedAttributes)
        {
            if (string.Equals(attribute.Name, attributeName, StringComparison.Ordinal)) return offset;
            offset = checked(offset + attribute.ComponentCount * sizeof(uint));
        }
        throw new InvalidDataException($"VFX particle buffer is missing required attribute '{attributeName}'.");
    }

    private static void AppendReadOnlyAttributeBufferHelpers(
        StringBuilder source,
        IReadOnlyList<VfxAttributeDefinition> storedAttributes,
        int aliveOffset)
    {
        int stride = checked(storedAttributes.Sum(attribute => attribute.ComponentCount * sizeof(uint)));
        source.Append("static const uint ANITY_VFX_ATTRIBUTE_STRIDE = ")
            .Append(stride.ToString(CultureInfo.InvariantCulture)).Append("u;\n")
            .Append("bool AnityVfxLoadAlive(uint particleIndex)\n")
            .Append("{\n")
            .Append("    uint baseAddress = particleIndex * ANITY_VFX_ATTRIBUTE_STRIDE;\n")
            .Append("    return attributeBuffer.Load(baseAddress + ")
            .Append(aliveOffset.ToString(CultureInfo.InvariantCulture)).Append("u) != 0u;\n")
            .Append("}\n")
            .Append("VFXAttributes AnityVfxLoadAttributes(uint particleIndex)\n")
            .Append("{\n")
            .Append("    uint baseAddress = particleIndex * ANITY_VFX_ATTRIBUTE_STRIDE;\n")
            .Append("    VFXAttributes attributes = (VFXAttributes)0;\n");
        int offset = 0;
        foreach (VfxAttributeDefinition attribute in storedAttributes)
        {
            source.Append("    attributes.").Append(attribute.Name).Append(" = ")
                .Append(AttributeLoadExpression(attribute, offset, "attributeBuffer")).Append(";\n");
            offset = checked(offset + attribute.ComponentCount * sizeof(uint));
        }
        source.Append("    return attributes;\n")
            .Append("}\n");
    }

    private static void AppendSourceAttributeBufferHelper(
        StringBuilder source,
        IReadOnlyList<VfxAttributeDefinition> sourceAttributes)
    {
        (IReadOnlyList<VfxAttributeLayoutField> fields, int stride) =
            VfxContextKernelCompilation.CreateSourceLayout(sourceAttributes);
        IReadOnlyDictionary<string, VfxAttributeLayoutField> fieldsByName = fields
            .ToDictionary(field => field.Name, StringComparer.Ordinal);
        source.Append("static const uint ANITY_VFX_SOURCE_ATTRIBUTE_STRIDE = ")
            .Append(stride.ToString(CultureInfo.InvariantCulture)).Append("u;\n")
            .Append("VFXSourceAttributes AnityVfxLoadSourceAttributes(uint sourceIndex)\n")
            .Append("{\n")
            .Append("    uint baseAddress = (startEventIndex + sourceIndex) * ANITY_VFX_SOURCE_ATTRIBUTE_STRIDE;\n")
            .Append("    VFXSourceAttributes sourceAttributes = (VFXSourceAttributes)0;\n");
        foreach (VfxAttributeDefinition attribute in sourceAttributes)
        {
            int offset = fieldsByName[attribute.Name].OffsetBytes;
            source.Append("    sourceAttributes.").Append(attribute.Name).Append(" = ")
                .Append(AttributeLoadExpression(attribute, offset, "sourceAttributeBuffer")).Append(";\n");
        }
        source.Append("    return sourceAttributes;\n")
            .Append("}\n");
    }

    private static string AttributeLoadExpression(
        VfxAttributeDefinition attribute,
        int offset,
        string bufferName)
    {
        string load = bufferName + ".Load" +
                      (attribute.ComponentCount == 1
                          ? string.Empty
                          : attribute.ComponentCount.ToString(CultureInfo.InvariantCulture)) +
                      "(baseAddress + " + offset.ToString(CultureInfo.InvariantCulture) + "u)";
        return attribute.ValueType switch
        {
            VfxAttributeValueType.Boolean => load + " != 0u",
            VfxAttributeValueType.UInt32 => load,
            VfxAttributeValueType.Int32 => "asint(" + load + ")",
            VfxAttributeValueType.Float or
                VfxAttributeValueType.Float2 or
                VfxAttributeValueType.Float3 or
                VfxAttributeValueType.Float4 => "asfloat(" + load + ")",
            _ => throw new ArgumentOutOfRangeException(nameof(attribute))
        };
    }

    private static string AttributeStoreExpression(VfxAttributeDefinition attribute)
        => attribute.ValueType switch
        {
            VfxAttributeValueType.Boolean => "attributes." + attribute.Name + " ? 1u : 0u",
            VfxAttributeValueType.UInt32 => "attributes." + attribute.Name,
            VfxAttributeValueType.Int32 or
                VfxAttributeValueType.Float or
                VfxAttributeValueType.Float2 or
                VfxAttributeValueType.Float3 or
                VfxAttributeValueType.Float4 => "asuint(attributes." + attribute.Name + ")",
            _ => throw new ArgumentOutOfRangeException(nameof(attribute))
        };

    private static IReadOnlyList<CompiledBlock> CompileLayoutBlocks(
        VfxTypedGraph graph,
        VfxContextDescriptor context,
        IReadOnlyDictionary<long, VfxSerializedAttributeUsage> usagesByModel)
    {
        var blocks = new List<CompiledBlock>();
        foreach (long childId in context.Model.ChildrenIds)
        {
            VfxModel child = graph.ModelsByFileId[childId];
            ActivationCompilation activation = CompileActivation(graph, child, ReadDisabled(child));
            if (!activation.IsIncluded) continue;
            if (child.Kind != VfxModelKind.Block)
                throw new NotSupportedException(
                    $"VFX {context.ContextType} layout does not support enabled child '{child.FileId}' type '{child.ScriptType.TypeName}'.");
            if (context.ContextType == VfxContextType.Init &&
                child.ScriptType.TypeName is not ("SetAttribute" or "SetCustomAttribute"))
                throw new NotSupportedException(
                    $"VFX Initialize layout does not support enabled child '{child.FileId}' type '{child.ScriptType.TypeName}'.");
            blocks.Add(CompileBlock(graph, child, usagesByModel, activation));
        }
        return blocks;
    }

    private static bool HasGpuEventSource(VfxTypedGraph graph, long contextId)
        => graph.FlowEdges
            .Where(edge => edge.TargetContextId == contextId)
            .Select(edge => graph.ModelsByFileId[edge.SourceContextId])
            .Any(source => source.ScriptType.TypeName == "VFXBasicGPUEvent");

    private static void AppendBlocks(StringBuilder source, IReadOnlyList<CompiledBlock> blocks, string indent)
    {
        foreach (CompiledBlock block in blocks)
        {
            source.Append(indent).Append("{\n");
            foreach (string declaration in block.ActivationDeclarations)
                source.Append(indent).Append("    ").Append(declaration).Append('\n');
            string bodyIndent = indent + "    ";
            if (block.ActivationCondition is not null)
            {
                source.Append(indent).Append("    if (").Append(block.ActivationCondition).Append(")\n")
                    .Append(indent).Append("    {\n");
                bodyIndent = indent + "        ";
            }
            if (block.Usage?.ValueSource == VfxAttributeValueSource.Source)
                source.Append(bodyIndent).Append(SourceValueDeclaration(block.Usage)).Append('\n');
            foreach (string declaration in block.InputDeclarations)
                source.Append(bodyIndent).Append(declaration).Append('\n');
            string statement = block.Statement;
            if (block.Usage?.RequiresRandomSeed == true) statement = ExpandRandomMacros(statement);
            foreach (string line in statement.Split('\n'))
                source.Append(bodyIndent).Append(line).Append('\n');
            if (block.ActivationCondition is not null)
                source.Append(indent).Append("    }\n");
            source.Append(indent).Append("}\n");
        }
    }

    private static CompiledBlock CompileBlock(
        VfxTypedGraph graph,
        VfxModel block,
        IReadOnlyDictionary<long, VfxSerializedAttributeUsage> usagesByModel,
        ActivationCompilation activation)
    {
        switch (block.ScriptType.TypeName)
        {
            case "SetAttribute":
            case "SetCustomAttribute":
            {
                VfxSerializedAttributeUsage usage = usagesByModel[block.FileId];
                return new CompiledBlock(
                    block,
                    usage,
                    CompileInputDeclarations(graph, block, RequiredInputNames(usage)),
                    activation,
                    usage.Attributes.Select(attribute => new AttributeAccess(attribute, usage.Mode)),
                    VfxAttributeCodeGenerator.GenerateSetAttributeStatement(usage, "attributes."));
            }
            case "Gravity":
                return new CompiledBlock(
                    block,
                    null,
                    CompileInputDeclarations(graph, block, new Dictionary<string, VfxSlotValueKind>
                    {
                        ["Force"] = VfxSlotValueKind.Vector
                    }),
                    activation,
                    new[] { Access("velocity", VfxAttributeMode.ReadWrite) },
                    "attributes.velocity += Force * deltaTime;");
            case "Force":
                return CompileForce(graph, block, activation);
            case "Drag":
                return CompileDrag(graph, block, activation);
            default:
                throw new NotSupportedException(
                    $"VFX Update kernel does not support enabled child '{block.FileId}' type '{block.ScriptType.TypeName}'.");
        }
    }

    private static CompiledBlock CompileForce(
        VfxTypedGraph graph,
        VfxModel block,
        ActivationCompilation activation)
    {
        int mode = VfxYamlFields.ReadInt32(block.Document.RawText, "Mode") ?? 0;
        IReadOnlyDictionary<string, VfxSlotValueKind> inputs = mode switch
        {
            0 => new Dictionary<string, VfxSlotValueKind> { ["Force"] = VfxSlotValueKind.Vector },
            1 => new Dictionary<string, VfxSlotValueKind>
            {
                ["Velocity"] = VfxSlotValueKind.Vector,
                ["Drag"] = VfxSlotValueKind.Float
            },
            _ => throw new InvalidDataException($"VFX Force block '{block.FileId}' has invalid Mode '{mode}'.")
        };
        string statement = mode == 0
            ? "attributes.velocity += (Force / attributes.mass) * deltaTime;"
            : "attributes.velocity += (Velocity - attributes.velocity) * min(1.0, Drag * deltaTime / attributes.mass);";
        return new CompiledBlock(
            block,
            null,
            CompileInputDeclarations(graph, block, inputs),
            activation,
            new[]
            {
                Access("velocity", VfxAttributeMode.ReadWrite),
                Access("mass", VfxAttributeMode.Read)
            },
            statement);
    }

    private static CompiledBlock CompileDrag(
        VfxTypedGraph graph,
        VfxModel block,
        ActivationCompilation activation)
    {
        bool useParticleSize = ReadBooleanSetting(block, "UseParticleSize", false);
        var accesses = new List<AttributeAccess>
        {
            Access("velocity", VfxAttributeMode.ReadWrite),
            Access("mass", VfxAttributeMode.Read)
        };
        string statement = string.Empty;
        if (useParticleSize)
        {
            accesses.Add(Access("size", VfxAttributeMode.Read));
            accesses.Add(Access("scaleX", VfxAttributeMode.Read));
            accesses.Add(Access("scaleY", VfxAttributeMode.Read));
            statement = "float2 side = attributes.size * float2(attributes.scaleX, attributes.scaleY);\n" +
                        "dragCoefficient *= side.x * side.y;\n";
        }
        statement += "attributes.velocity *= max(0.0, (1.0 - (dragCoefficient * deltaTime) / attributes.mass));";
        return new CompiledBlock(
            block,
            null,
            CompileInputDeclarations(graph, block, new Dictionary<string, VfxSlotValueKind>
            {
                ["dragCoefficient"] = VfxSlotValueKind.Float
            }),
            activation,
            accesses,
            statement);
    }

    private static ImplicitUpdateCompilation CompileImplicitUpdate(
        IReadOnlyList<AttributeAccess> explicitAccesses,
        IReadOnlyList<VfxAttributeDefinition> storedAttributes,
        UpdateSettings settings)
    {
        bool IsStored(string name) => storedAttributes.Any(attribute => attribute.Name == name);
        bool IsRead(string name) => explicitAccesses.Any(access =>
            access.Attribute.Name == name && (access.Mode & VfxAttributeMode.Read) != 0);

        var accesses = new List<AttributeAccess>();
        var pre = new List<string>();
        var post = new List<string>();
        if (IsRead("oldPosition"))
        {
            accesses.Add(Access("position", VfxAttributeMode.Read));
            accesses.Add(Access("oldPosition", VfxAttributeMode.Write));
            pre.Add("attributes.oldPosition = attributes.position;");
        }
        if (settings.Integration == 0 && IsStored("velocity"))
        {
            accesses.Add(Access("position", VfxAttributeMode.ReadWrite));
            accesses.Add(Access("velocity", VfxAttributeMode.Read));
            post.Add("attributes.position += attributes.velocity * deltaTime;");
        }
        if (settings.AngularIntegration == 0)
        {
            AddAngularIntegration("X");
            AddAngularIntegration("Y");
            AddAngularIntegration("Z");
        }

        bool hasAge = IsStored("age") ||
            IsStored("lifetime") && (settings.AgeParticles || settings.ReapParticles);
        bool hasLifetime = IsStored("lifetime");
        bool hasAlive = IsStored("alive") || hasLifetime && settings.ReapParticles;
        if (hasAge)
        {
            if (settings.AgeParticles)
            {
                accesses.Add(Access("age", VfxAttributeMode.ReadWrite));
                post.Add("attributes.age += deltaTime;");
            }
            if (hasLifetime && hasAlive && settings.ReapParticles)
            {
                accesses.Add(Access("age", VfxAttributeMode.Read));
                accesses.Add(Access("lifetime", VfxAttributeMode.Read));
                accesses.Add(Access("alive", VfxAttributeMode.ReadWrite));
                post.Add("if (attributes.age > attributes.lifetime) { attributes.alive = false; }");
            }
        }
        return new ImplicitUpdateCompilation(accesses, pre, post);

        void AddAngularIntegration(string axis)
        {
            string angularVelocity = "angularVelocity" + axis;
            string angle = "angle" + axis;
            if (!IsStored(angularVelocity)) return;
            accesses.Add(Access(angle, VfxAttributeMode.ReadWrite));
            accesses.Add(Access(angularVelocity, VfxAttributeMode.Read));
            post.Add($"attributes.{angle} += attributes.{angularVelocity} * deltaTime;");
        }
    }

    private static UpdateSettings ReadUpdateSettings(VfxModel context)
    {
        int integration = ReadEnumSetting(context, "integration", 0, 0, 1);
        int angularIntegration = ReadEnumSetting(context, "angularIntegration", 0, 0, 1);
        return new UpdateSettings(
            integration,
            angularIntegration,
            ReadBooleanSetting(context, "ageParticles", true),
            ReadBooleanSetting(context, "reapParticles", true),
            ReadBooleanSetting(context, "skipZeroDeltaUpdate", false));
    }

    private static int ReadEnumSetting(VfxModel model, string name, int defaultValue, params int[] validValues)
    {
        int value = VfxYamlFields.ReadInt32(model.Document.RawText, name) ?? defaultValue;
        if (!validValues.Contains(value))
            throw new InvalidDataException($"VFX model '{model.FileId}' field {name} has invalid value '{value}'.");
        return value;
    }

    private static bool ReadBooleanSetting(VfxModel model, string name, bool defaultValue)
    {
        int value = VfxYamlFields.ReadInt32(model.Document.RawText, name) ?? (defaultValue ? 1 : 0);
        if (value is not (0 or 1))
            throw new InvalidDataException($"VFX model '{model.FileId}' field {name} must be 0 or 1.");
        return value == 1;
    }

    private static AttributeAccess Access(string name, VfxAttributeMode mode)
        => new(VfxAttributeCatalog.Find(name), mode);

    private static CompiledInputs CompileInputDeclarations(
        VfxTypedGraph graph,
        VfxModel block,
        IReadOnlyCollection<string> requiredNames)
        => CompileInputDeclarations(graph, block, requiredNames.ToDictionary(name => name, _ => (VfxSlotValueKind?)null));

    private static CompiledInputs CompileInputDeclarations(
        VfxTypedGraph graph,
        VfxModel block,
        IReadOnlyDictionary<string, VfxSlotValueKind> requiredTypes)
        => CompileInputDeclarations(graph, block, requiredTypes.ToDictionary(pair => pair.Key, pair => (VfxSlotValueKind?)pair.Value));

    private static CompiledInputs CompileInputDeclarations(
        VfxTypedGraph graph,
        VfxModel block,
        IReadOnlyDictionary<string, VfxSlotValueKind?> requiredTypes)
    {
        var declarations = new List<string>(block.InputSlotIds.Count);
        var dependencies = new List<VfxExpressionAttributeDependency>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (long slotId in block.InputSlotIds)
        {
            VfxModel slot = graph.ModelsByFileId[slotId];
            VfxSlotProperty property = slot.SlotProperty
                ?? throw new InvalidDataException($"VFX block input slot '{slotId}' has no typed property.");
            if (!VfxAttributeCatalog.IsShaderIdentifier(property.Name))
                throw new InvalidDataException(
                    $"VFX block input slot '{slotId}' name '{property.Name}' is not a shader identifier.");
            if (!names.Add(property.Name))
                throw new InvalidDataException(
                    $"VFX block '{block.FileId}' contains duplicate input name '{property.Name}'.");
            if (requiredTypes.TryGetValue(property.Name, out VfxSlotValueKind? expectedKind) &&
                expectedKind is not null && property.Value.Kind != expectedKind.Value)
                throw new InvalidDataException(
                    $"VFX block '{block.FileId}' input '{property.Name}' must be {expectedKind.Value}, not {property.Value.Kind}.");
            if (slot.LinkedSlotIds.Count == 0)
                declarations.Add(HlslDeclaration(property.Name, property.Value));
            else
                AddLinkedExpressionDeclarations(declarations, dependencies, graph, slot, property.Name);
        }
        foreach (string requiredName in requiredTypes.Keys)
        {
            if (!names.Contains(requiredName))
                throw new InvalidDataException(
                    $"VFX block '{block.FileId}' is missing required input slot '{requiredName}'.");
        }
        return new CompiledInputs(declarations.AsReadOnly(), dependencies.AsReadOnly());
    }

    private static void AddLinkedExpressionDeclarations(
        ICollection<string> declarations,
        ICollection<VfxExpressionAttributeDependency> dependencies,
        VfxTypedGraph graph,
        VfxModel inputSlot,
        string propertyName)
    {
        VfxExpressionCompilation expression = VfxExpressionCompiler.CompileInput(graph, inputSlot.FileId);
        AddExpressionDeclarations(declarations, dependencies, expression, propertyName);
    }

    private static void AddExpressionDeclarations(
        ICollection<string> declarations,
        ICollection<VfxExpressionAttributeDependency> dependencies,
        VfxExpressionCompilation expression,
        string propertyName)
    {
        foreach (VfxExpressionAttributeDependency dependency in expression.AttributeDependencies)
            dependencies.Add(dependency);
        declarations.Add(expression.HlslType + " " + propertyName + ";");
        declarations.Add("{");
        foreach (string line in expression.HlslSource.Split('\n'))
        {
            if (line.Length != 0) declarations.Add("    " + line);
        }
        declarations.Add("    " + propertyName + " = " + expression.ResultVariable + ";");
        declarations.Add("}");
    }

    private static IReadOnlyList<string> RequiredInputNames(VfxSerializedAttributeUsage usage)
    {
        var names = new List<string>();
        if (usage.ValueSource == VfxAttributeValueSource.Source)
        {
            // Unity generates Value from the context-entry source attribute snapshot.
        }
        else if (usage.IsCustom)
        {
            if (usage.RandomMode == VfxAttributeRandomMode.Off)
                names.Add("_" + PascalCase(usage.SerializedAttributeName));
            else
            {
                names.Add("Min");
                names.Add("Max");
            }
        }
        else if (usage.RandomMode == VfxAttributeRandomMode.Off)
            names.Add(PascalCase(usage.SerializedAttributeName));
        else
        {
            names.Add("A");
            names.Add("B");
        }
        if (usage.Composition == VfxAttributeComposition.Blend) names.Add("Blend");
        return names.AsReadOnly();
    }

    private static IReadOnlyList<VfxAttributeDefinition> DistinctAttributes(
        IEnumerable<VfxAttributeDefinition> attributes)
        => attributes
            .GroupBy(attribute => attribute.Name, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(attribute => VfxAttributeCatalog.Stored
                .Select((item, index) => (item.Name, index))
                .FirstOrDefault(item => item.Name == attribute.Name).index)
            .ThenBy(attribute => attribute.Name, StringComparer.Ordinal)
            .ToArray();

    private static string SourceValueDeclaration(VfxSerializedAttributeUsage usage)
    {
        VfxAttributeDefinition serialized = VfxAttributeCatalog.Find(usage.SerializedAttributeName);
        if (serialized.Variadic != VfxAttributeVariadic.True)
        {
            VfxAttributeDefinition attribute = usage.Attributes.Single();
            return attribute.HlslType + " Value = sourceAttributes." + attribute.Name + ";";
        }

        int count = usage.Attributes.Count;
        if (count is < 1 or > 3)
            throw new InvalidDataException(
                $"VFX source variadic attribute '{usage.SerializedAttributeName}' requires one to three channels.");
        string hlslType = count == 1 ? "float" : "float" + count.ToString(CultureInfo.InvariantCulture);
        string expression = count == 1
            ? "sourceAttributes." + usage.Attributes[0].Name
            : hlslType + "(" + string.Join(", ", usage.Attributes.Select(attribute =>
                "sourceAttributes." + attribute.Name)) + ")";
        return hlslType + " Value = " + expression + ";";
    }

    private static string PascalCase(string name)
        => char.ToUpperInvariant(name[0]) + name.Substring(1);

    private static string HlslDeclaration(string name, VfxSlotValue value)
        => value.Kind switch
        {
            VfxSlotValueKind.Float => "float " + name + " = " + HlslLiteral(Required(value.Scalar, name)) + ";",
            VfxSlotValueKind.Int32 => "int " + name + " = " + Required(value.SignedInteger, name).ToString(CultureInfo.InvariantCulture) + ";",
            VfxSlotValueKind.UInt32 => "uint " + name + " = " + Required(value.UnsignedInteger, name).ToString(CultureInfo.InvariantCulture) + "u;",
            VfxSlotValueKind.Boolean => "bool " + name + " = " + (Required(value.Boolean, name) ? "true" : "false") + ";",
            VfxSlotValueKind.Float2 => VectorDeclaration("float2", name, value.Components, 2),
            VfxSlotValueKind.Float4 => VectorDeclaration("float4", name, value.Components, 4),
            VfxSlotValueKind.Float3 or VfxSlotValueKind.Position or VfxSlotValueKind.Direction or VfxSlotValueKind.Vector
                => VectorDeclaration("float3", name, value.Components, 3),
            _ => throw new NotSupportedException(
                $"VFX Update kernel does not support input '{name}' value kind '{value.Kind}'.")
        };

    private static string VectorDeclaration(
        string type,
        string name,
        IReadOnlyList<double> components,
        int expectedCount)
    {
        if (components.Count != expectedCount)
            throw new InvalidDataException($"VFX input '{name}' requires {expectedCount} components.");
        return type + " " + name + " = " + type + "(" +
               string.Join(", ", components.Select(HlslLiteral)) + ");";
    }

    private static bool ReadDisabled(VfxModel block)
    {
        int disabled = VfxYamlFields.ReadInt32(block.Document.RawText, "m_Disabled") ?? 0;
        if (disabled is not (0 or 1))
            throw new InvalidDataException($"VFX block '{block.FileId}' m_Disabled must be 0 or 1.");
        return disabled == 1;
    }

    private static ActivationCompilation CompileActivation(
        VfxTypedGraph graph,
        VfxModel block,
        bool legacyDisabled)
    {
        long activationId = VfxYamlFields.ReadReference(block.Document.RawText, "m_ActivationSlot");
        if (activationId == 0) return ActivationCompilation.Constant(!legacyDisabled);
        if (!graph.ModelsByFileId.TryGetValue(activationId, out VfxModel? activation) ||
            activation.Kind != VfxModelKind.Slot || activation.OwnerId != block.FileId)
            throw new InvalidDataException(
                $"VFX block '{block.FileId}' has invalid activation slot '{activationId}'.");
        VfxSlotValue value = activation.SlotProperty?.Value
            ?? throw new InvalidDataException($"VFX activation slot '{activationId}' has no typed value.");
        if (value.Kind != VfxSlotValueKind.Boolean || value.Boolean is null)
            throw new InvalidDataException($"VFX activation slot '{activationId}' must be Boolean.");
        if (activation.LinkedSlotIds.Count == 0) return ActivationCompilation.Constant(value.Boolean.Value);

        VfxExpressionCompilation expression = VfxExpressionCompiler.CompileInput(graph, activationId);
        if (expression.ResultType != VfxExpressionValueType.Boolean || expression.HlslType != "bool")
            throw new InvalidDataException($"VFX linked activation slot '{activationId}' must compile to Boolean.");
        var declarations = new List<string>();
        var dependencies = new List<VfxExpressionAttributeDependency>();
        AddExpressionDeclarations(declarations, dependencies, expression, "_vfx_enabled");
        return ActivationCompilation.Dynamic(declarations, dependencies);
    }

    private static string ExpandRandomMacros(string statement)
        => statement
            .Replace("RAND4", RandomVector(4), StringComparison.Ordinal)
            .Replace("RAND3", RandomVector(3), StringComparison.Ordinal)
            .Replace("RAND2", RandomVector(2), StringComparison.Ordinal)
            .Replace("RAND", "AnityVfxRandom(randomState)", StringComparison.Ordinal);

    private static string RandomVector(int count)
        => "float" + count.ToString(CultureInfo.InvariantCulture) + "(" +
           string.Join(", ", Enumerable.Repeat("AnityVfxRandom(randomState)", count)) + ")";

    private static string HlslLiteral(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new InvalidDataException("VFX kernel HLSL does not support NaN or infinity constants.");
        string literal = value.ToString("R", CultureInfo.InvariantCulture).Replace("E", "e");
        if (literal.IndexOf('.') < 0 && literal.IndexOf('e') < 0) literal += ".0";
        return literal;
    }

    private static T Required<T>(T? value, string name) where T : struct
        => value ?? throw new InvalidDataException($"VFX input '{name}' has no typed value.");

    private sealed class CompiledBlock
    {
        internal CompiledBlock(
            VfxModel model,
            VfxSerializedAttributeUsage? usage,
            CompiledInputs inputs,
            ActivationCompilation activation,
            IEnumerable<AttributeAccess> attributeAccesses,
            string statement)
        {
            Model = model;
            Usage = usage;
            InputDeclarations = inputs.Declarations;
            ActivationDeclarations = activation.Declarations;
            ActivationCondition = activation.Condition;
            AttributeAccesses = attributeAccesses.ToArray();
            Statement = statement;
            AttributeDependencies = inputs.AttributeDependencies
                .Concat(activation.AttributeDependencies)
                .ToArray();
        }

        internal VfxModel Model { get; }
        internal VfxSerializedAttributeUsage? Usage { get; }
        internal IReadOnlyList<string> InputDeclarations { get; }
        internal IReadOnlyList<string> ActivationDeclarations { get; }
        internal string? ActivationCondition { get; }
        internal IReadOnlyList<AttributeAccess> AttributeAccesses { get; }
        internal IReadOnlyList<VfxExpressionAttributeDependency> AttributeDependencies { get; }
        internal string Statement { get; }
    }

    private readonly struct AttributeAccess
    {
        internal AttributeAccess(VfxAttributeDefinition attribute, VfxAttributeMode mode)
        {
            Attribute = attribute;
            Mode = mode;
        }

        internal VfxAttributeDefinition Attribute { get; }
        internal VfxAttributeMode Mode { get; }
    }

    private sealed class ImplicitUpdateCompilation
    {
        internal ImplicitUpdateCompilation(
            IReadOnlyList<AttributeAccess> attributeAccesses,
            IReadOnlyList<string> preStatements,
            IReadOnlyList<string> postStatements)
        {
            AttributeAccesses = attributeAccesses;
            PreStatements = preStatements;
            PostStatements = postStatements;
        }

        internal IReadOnlyList<AttributeAccess> AttributeAccesses { get; }
        internal IReadOnlyList<string> PreStatements { get; }
        internal IReadOnlyList<string> PostStatements { get; }
    }

    private readonly struct UpdateSettings
    {
        internal UpdateSettings(
            int integration,
            int angularIntegration,
            bool ageParticles,
            bool reapParticles,
            bool skipZeroDeltaUpdate)
        {
            Integration = integration;
            AngularIntegration = angularIntegration;
            AgeParticles = ageParticles;
            ReapParticles = reapParticles;
            SkipZeroDeltaUpdate = skipZeroDeltaUpdate;
        }

        internal int Integration { get; }
        internal int AngularIntegration { get; }
        internal bool AgeParticles { get; }
        internal bool ReapParticles { get; }
        internal bool SkipZeroDeltaUpdate { get; }
    }

    private sealed class CompiledInputs
    {
        internal CompiledInputs(
            IReadOnlyList<string> declarations,
            IReadOnlyList<VfxExpressionAttributeDependency> attributeDependencies)
        {
            Declarations = declarations;
            AttributeDependencies = attributeDependencies;
        }

        internal IReadOnlyList<string> Declarations { get; }
        internal IReadOnlyList<VfxExpressionAttributeDependency> AttributeDependencies { get; }
    }

    private sealed class ActivationCompilation
    {
        private ActivationCompilation(
            bool isIncluded,
            string? condition,
            IReadOnlyList<string> declarations,
            IReadOnlyList<VfxExpressionAttributeDependency> attributeDependencies)
        {
            IsIncluded = isIncluded;
            Condition = condition;
            Declarations = declarations;
            AttributeDependencies = attributeDependencies;
        }

        internal bool IsIncluded { get; }
        internal string? Condition { get; }
        internal IReadOnlyList<string> Declarations { get; }
        internal IReadOnlyList<VfxExpressionAttributeDependency> AttributeDependencies { get; }

        internal static ActivationCompilation Constant(bool enabled)
            => new(enabled, null, Array.Empty<string>(), Array.Empty<VfxExpressionAttributeDependency>());

        internal static ActivationCompilation Dynamic(
            IReadOnlyList<string> declarations,
            IReadOnlyList<VfxExpressionAttributeDependency> dependencies)
            => new(true, "_vfx_enabled", declarations, dependencies);
    }

    private const string RandomHelpers =
        "uint AnityVfxHash(uint value)\n" +
        "{\n" +
        "    value = (value ^ 61u) ^ (value >> 16);\n" +
        "    value += value << 3;\n" +
        "    value ^= value >> 4;\n" +
        "    value *= 0x27d4eb2du;\n" +
        "    value ^= value >> 15;\n" +
        "    return value;\n" +
        "}\n\n" +
        "float AnityVfxRandom(inout uint state)\n" +
        "{\n" +
        "    state = AnityVfxHash(state);\n" +
        "    return (state & 0x00ffffffu) * (1.0 / 16777216.0);\n" +
        "}\n";

    private const string PrefixSumBinarySearchHelper =
        "uint AnityVfxFindSourceIndex(uint spawnIndex, uint sourceEventCount)\n" +
        "{\n" +
        "    if (sourceEventCount == 0u) return 0u;\n" +
        "    uint low = 0u;\n" +
        "    uint high = sourceEventCount;\n" +
        "    while (low < high)\n" +
        "    {\n" +
        "        uint middle = low + ((high - low) >> 1u);\n" +
        "        if (spawnIndex < spawnCountPrefixSum[middle]) high = middle;\n" +
        "        else low = middle + 1u;\n" +
        "    }\n" +
        "    return min(low, sourceEventCount - 1u);\n" +
        "}\n";

    private const string PlanarTransformHelpers =
        "float3x3 AnityVfxGetScaleMatrix(float3 scale)\n" +
        "{\n" +
        "    return float3x3(scale.x, 0.0, 0.0, 0.0, scale.y, 0.0, 0.0, 0.0, scale.z);\n" +
        "}\n\n" +
        "float3x3 AnityVfxGetEulerMatrix(float3 angles)\n" +
        "{\n" +
        "    float3 s;\n" +
        "    float3 c;\n" +
        "    sincos(angles, s, c);\n" +
        "    return float3x3(\n" +
        "        c.y * c.z + s.x * s.y * s.z, c.z * s.x * s.y - c.y * s.z, c.x * s.y,\n" +
        "        c.x * s.z, c.x * c.z, -s.x,\n" +
        "        -c.z * s.y + c.y * s.x * s.z, c.y * c.z * s.x + s.y * s.z, c.x * c.y);\n" +
        "}\n\n" +
        "float4x4 AnityVfxGetElementToVFXMatrix(\n" +
        "    float3 axisX, float3 axisY, float3 axisZ, float3 angles,\n" +
        "    float3 pivot, float3 size, float3 position)\n" +
        "{\n" +
        "    float3x3 rotationAndScale = AnityVfxGetScaleMatrix(size);\n" +
        "    rotationAndScale = mul(AnityVfxGetEulerMatrix(radians(angles)), rotationAndScale);\n" +
        "    rotationAndScale = mul(transpose(float3x3(axisX, axisY, axisZ)), rotationAndScale);\n" +
        "    position -= mul(rotationAndScale, pivot);\n" +
        "    return float4x4(\n" +
        "        float4(rotationAndScale[0], position.x),\n" +
        "        float4(rotationAndScale[1], position.y),\n" +
        "        float4(rotationAndScale[2], position.z),\n" +
        "        float4(0.0, 0.0, 0.0, 1.0));\n" +
        "}\n\n" +
        "float2 AnityVfxGetSubUV(float flipbookIndex, float2 uv, float2 dimensions, float2 inverseDimensions)\n" +
        "{\n" +
        "    float2 tile = float2(fmod(flipbookIndex, dimensions.x),\n" +
        "        dimensions.y - 1.0 - floor(flipbookIndex * inverseDimensions.x));\n" +
        "    return (tile + uv) * inverseDimensions;\n" +
        "}\n";
}

internal enum VfxKernelStage
{
    Cpu,
    Compute,
    Vertex
}

internal enum VfxPlanarBlendMode
{
    Additive,
    Alpha,
    AlphaPremultiplied,
    Opaque
}

internal enum VfxPlanarCullMode
{
    Off,
    Front,
    Back
}

internal enum VfxPlanarZTest
{
    Less,
    Greater,
    LEqual,
    GEqual,
    Equal,
    NotEqual,
    Always
}

internal sealed class VfxPlanarRenderState
{
    internal VfxPlanarRenderState(
        VfxPlanarBlendMode blendMode,
        VfxPlanarCullMode cullMode,
        bool zWrite,
        VfxPlanarZTest zTest,
        bool alphaClipping,
        string renderQueue,
        bool requiresSorting,
        bool indirectDraw)
    {
        BlendMode = blendMode;
        CullMode = cullMode;
        ZWrite = zWrite;
        ZTest = zTest;
        AlphaClipping = alphaClipping;
        RenderQueue = renderQueue;
        RequiresSorting = requiresSorting;
        IndirectDraw = indirectDraw;
    }

    internal VfxPlanarBlendMode BlendMode { get; }
    internal VfxPlanarCullMode CullMode { get; }
    internal bool ZWrite { get; }
    internal VfxPlanarZTest ZTest { get; }
    internal bool AlphaClipping { get; }
    internal string RenderQueue { get; }
    internal bool RequiresSorting { get; }
    internal bool IndirectDraw { get; }

    internal bool BlendEnabled => BlendMode != VfxPlanarBlendMode.Opaque;
    internal string SourceBlendFactor => BlendMode switch
    {
        VfxPlanarBlendMode.Additive or VfxPlanarBlendMode.Alpha => "SrcAlpha",
        VfxPlanarBlendMode.AlphaPremultiplied => "One",
        VfxPlanarBlendMode.Opaque => "One",
        _ => throw new InvalidOperationException()
    };
    internal string DestinationBlendFactor => BlendMode switch
    {
        VfxPlanarBlendMode.Additive => "One",
        VfxPlanarBlendMode.Alpha or VfxPlanarBlendMode.AlphaPremultiplied => "OneMinusSrcAlpha",
        VfxPlanarBlendMode.Opaque => "Zero",
        _ => throw new InvalidOperationException()
    };
}

internal sealed class VfxContextKernelCompilation
{
    internal VfxContextKernelCompilation(
        long contextId,
        string hlslSource,
        IReadOnlyList<long> compiledBlockIds,
        bool usesRandom,
        bool usesSource,
        bool usesDeadList,
        bool usesGpuEventSource,
        bool usesExternalSourceBuffer,
        IReadOnlyList<VfxAttributeDefinition> storedAttributes,
        IReadOnlyList<VfxAttributeDefinition> sourceAttributes,
        int threadGroupSize,
        VfxKernelStage stage = VfxKernelStage.Compute,
        int verticesPerParticle = 0,
        bool usesReadOnlyAttributeBuffer = false,
        string? vertexEntryPoint = null,
        string? fragmentEntryPoint = null,
        IReadOnlyList<int>? indexPattern = null,
        VfxPlanarRenderState? planarRenderState = null,
        string? outputEventName = null,
        IReadOnlyList<long>? outputEventContextIds = null,
        IReadOnlyList<VfxOutputEventBufferMapping>? outputEventBufferMappings = null,
        IReadOnlyList<VfxAttributeDefinition>? outputEventAttributes = null,
        VfxAttributeMode outputEventAttributeMode = VfxAttributeMode.None,
        bool disablesInstancing = false,
        bool runtimeExecutable = true)
    {
        ContextId = contextId;
        HlslSource = hlslSource;
        CompiledBlockIds = new ReadOnlyCollection<long>(compiledBlockIds.ToArray());
        UsesRandom = usesRandom;
        UsesSource = usesSource;
        UsesDeadList = usesDeadList;
        UsesGpuEventSource = usesGpuEventSource;
        UsesExternalSourceBuffer = usesExternalSourceBuffer;
        (AttributeLayout, AttributeStrideBytes) = CreateLayout(storedAttributes);
        (SourceAttributeLayout, SourceAttributeStrideBytes) = usesExternalSourceBuffer
            ? CreateSourceLayout(sourceAttributes)
            : CreateLayout(sourceAttributes);
        StoredAttributes = new ReadOnlyCollection<VfxAttributeDefinition>(storedAttributes.ToArray());
        SourceAttributes = new ReadOnlyCollection<VfxAttributeDefinition>(sourceAttributes.ToArray());
        ThreadGroupSize = threadGroupSize;
        Stage = stage;
        VerticesPerParticle = verticesPerParticle;
        UsesReadOnlyAttributeBuffer = usesReadOnlyAttributeBuffer;
        VertexEntryPoint = vertexEntryPoint;
        FragmentEntryPoint = fragmentEntryPoint;
        IndexPattern = new ReadOnlyCollection<int>((indexPattern ?? Array.Empty<int>()).ToArray());
        PlanarRenderState = planarRenderState;
        OutputEventName = outputEventName;
        OutputEventContextIds = new ReadOnlyCollection<long>(
            (outputEventContextIds ?? Array.Empty<long>()).ToArray());
        OutputEventBufferMappings = new ReadOnlyCollection<VfxOutputEventBufferMapping>(
            (outputEventBufferMappings ?? Array.Empty<VfxOutputEventBufferMapping>()).ToArray());
        OutputEventAttributes = new ReadOnlyCollection<VfxAttributeDefinition>(
            (outputEventAttributes ?? Array.Empty<VfxAttributeDefinition>()).ToArray());
        OutputEventAttributeMode = outputEventAttributeMode;
        DisablesInstancing = disablesInstancing;
        RuntimeExecutable = runtimeExecutable;
    }

    private static (IReadOnlyList<VfxAttributeLayoutField> Fields, int Stride) CreateLayout(
        IReadOnlyList<VfxAttributeDefinition> attributes)
    {
        var fields = new List<VfxAttributeLayoutField>(attributes.Count);
        int offset = 0;
        foreach (VfxAttributeDefinition attribute in attributes)
        {
            int size = checked(attribute.ComponentCount * sizeof(uint));
            fields.Add(new VfxAttributeLayoutField(attribute.Name, attribute.HlslType, offset, size));
            offset = checked(offset + size);
        }
        return (new ReadOnlyCollection<VfxAttributeLayoutField>(fields), offset);
    }

    internal static (IReadOnlyList<VfxAttributeLayoutField> Fields, int Stride) CreateSourceLayout(
        IReadOnlyList<VfxAttributeDefinition> attributes)
    {
        var blocks = new List<List<VfxAttributeDefinition>>();
        foreach (VfxAttributeDefinition attribute in attributes.OrderByDescending(attribute => attribute.ComponentCount))
        {
            List<VfxAttributeDefinition>? block = blocks.FirstOrDefault(candidate =>
                candidate.Sum(item => item.ComponentCount) + attribute.ComponentCount <= 4);
            if (block is null)
            {
                block = new List<VfxAttributeDefinition>();
                blocks.Add(block);
            }
            block.Add(attribute);
        }

        var offsets = new Dictionary<string, int>(StringComparer.Ordinal);
        int currentOffsetWords = 0;
        int minimumAlignmentWords = 0;
        foreach (IReadOnlyList<VfxAttributeDefinition> block in blocks)
        {
            foreach (VfxAttributeDefinition attribute in block)
            {
                int sizeWords = attribute.ComponentCount;
                int alignmentWords = sizeWords > 2 ? 4 : sizeWords;
                minimumAlignmentWords = Math.Max(minimumAlignmentWords, alignmentWords);
                currentOffsetWords = Align(currentOffsetWords, alignmentWords);
                offsets.Add(attribute.Name, checked(currentOffsetWords * sizeof(uint)));
                currentOffsetWords = checked(currentOffsetWords + sizeWords);
            }
        }
        if (minimumAlignmentWords != 0)
            currentOffsetWords = Align(currentOffsetWords, minimumAlignmentWords);

        VfxAttributeLayoutField[] fields = attributes.Select(attribute =>
                new VfxAttributeLayoutField(
                    attribute.Name,
                    attribute.HlslType,
                    offsets[attribute.Name],
                    checked(attribute.ComponentCount * sizeof(uint))))
            .ToArray();
        return (new ReadOnlyCollection<VfxAttributeLayoutField>(fields),
            checked(currentOffsetWords * sizeof(uint)));
    }

    private static int Align(int value, int alignment)
        => checked((value + alignment - 1) & ~(alignment - 1));

    internal long ContextId { get; }
    internal string HlslSource { get; }
    internal IReadOnlyList<long> CompiledBlockIds { get; }
    internal bool UsesRandom { get; }
    internal bool UsesSource { get; }
    internal bool UsesDeadList { get; }
    internal bool UsesGpuEventSource { get; }
    internal bool UsesExternalSourceBuffer { get; }
    internal bool UsesBatchedSourceEventOffset => UsesExternalSourceBuffer;
    internal IReadOnlyList<VfxAttributeLayoutField> AttributeLayout { get; }
    internal int AttributeStrideBytes { get; }
    internal IReadOnlyList<VfxAttributeLayoutField> SourceAttributeLayout { get; }
    internal int SourceAttributeStrideBytes { get; }
    internal IReadOnlyList<VfxAttributeDefinition> StoredAttributes { get; }
    internal IReadOnlyList<VfxAttributeDefinition> SourceAttributes { get; }
    internal int ThreadGroupSize { get; }
    internal VfxKernelStage Stage { get; }
    internal int VerticesPerParticle { get; }
    internal bool UsesReadOnlyAttributeBuffer { get; }
    internal string? VertexEntryPoint { get; }
    internal string? FragmentEntryPoint { get; }
    internal IReadOnlyList<int> IndexPattern { get; }
    internal VfxPlanarRenderState? PlanarRenderState { get; }
    internal string? OutputEventName { get; }
    internal IReadOnlyList<long> OutputEventContextIds { get; }
    internal IReadOnlyList<VfxOutputEventBufferMapping> OutputEventBufferMappings { get; }
    internal IReadOnlyList<VfxAttributeDefinition> OutputEventAttributes { get; }
    internal VfxAttributeMode OutputEventAttributeMode { get; }
    internal bool DisablesInstancing { get; }
    internal bool RuntimeExecutable { get; }

    internal int GetPlanarVertexCount(int particleCount)
    {
        if (particleCount < 0) throw new ArgumentOutOfRangeException(nameof(particleCount));
        if (VerticesPerParticle == 0)
            throw new InvalidOperationException("VFX compilation is not a Planar Output draw.");
        return checked(particleCount * VerticesPerParticle);
    }

    internal int GetPlanarIndexCount(int particleCount)
    {
        if (particleCount < 0) throw new ArgumentOutOfRangeException(nameof(particleCount));
        if (IndexPattern.Count == 0)
            throw new InvalidOperationException("VFX compilation is not an indexed Planar Output draw.");
        return checked(particleCount * IndexPattern.Count);
    }

    internal uint[] BuildPlanarIndexBuffer(int particleCount)
    {
        int count = GetPlanarIndexCount(particleCount);
        var indices = new uint[count];
        int destination = 0;
        for (int particleIndex = 0; particleIndex < particleCount; particleIndex++)
        {
            uint baseVertex = checked((uint)(particleIndex * VerticesPerParticle));
            foreach (int localIndex in IndexPattern)
                indices[destination++] = checked(baseVertex + (uint)localIndex);
        }
        return indices;
    }

    internal VfxEventAttributeLayout CreateOutputEventRecordLayout()
    {
        if (Stage != VfxKernelStage.Cpu || OutputEventName is null)
            throw new InvalidOperationException("VFX compilation is not an Output Event system descriptor.");
        return VfxEventAttributeLayout.Create(OutputEventAttributes);
    }
}

internal sealed class VfxOutputEventBufferMapping
{
    internal VfxOutputEventBufferMapping(string name, long sourceSpawnerContextId)
    {
        Name = name;
        SourceSpawnerContextId = sourceSpawnerContextId;
    }

    internal string Name { get; }
    internal long SourceSpawnerContextId { get; }
}

internal sealed class VfxAttributeLayoutField
{
    internal VfxAttributeLayoutField(string name, string hlslType, int offsetBytes, int sizeBytes)
    {
        Name = name;
        HlslType = hlslType;
        OffsetBytes = offsetBytes;
        SizeBytes = sizeBytes;
    }

    internal string Name { get; }
    internal string HlslType { get; }
    internal int OffsetBytes { get; }
    internal int SizeBytes { get; }
}
