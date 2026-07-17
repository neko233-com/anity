using System.Collections.ObjectModel;
using System.Globalization;
using UnityEditor.VFX.Serialization;

namespace UnityEditor.VFX.Model;

internal sealed class VfxTypedGraph
{
    private readonly ReadOnlyCollection<VfxModel> _models;
    private readonly ReadOnlyDictionary<long, VfxModel> _modelsByFileId;
    private readonly ReadOnlyCollection<VfxFlowEdge> _flowEdges;

    private VfxTypedGraph(
        VfxYamlAsset asset,
        VfxModel graph,
        List<VfxModel> models,
        Dictionary<long, VfxModel> modelsByFileId,
        List<VfxFlowEdge> flowEdges)
    {
        Asset = asset;
        Graph = graph;
        _models = models.AsReadOnly();
        _modelsByFileId = new ReadOnlyDictionary<long, VfxModel>(modelsByFileId);
        _flowEdges = flowEdges.AsReadOnly();
    }

    internal VfxYamlAsset Asset { get; }

    internal VfxModel Graph { get; }

    internal IReadOnlyList<VfxModel> Models => _models;

    internal IReadOnlyDictionary<long, VfxModel> ModelsByFileId => _modelsByFileId;

    internal IReadOnlyList<VfxModel> Contexts => _models.Where(model => model.Kind == VfxModelKind.Context).ToArray();

    internal IReadOnlyList<VfxModel> Blocks => _models.Where(model => model.Kind == VfxModelKind.Block).ToArray();

    internal IReadOnlyList<VfxModel> Operators => _models.Where(model => model.Kind == VfxModelKind.Operator).ToArray();

    internal IReadOnlyList<VfxModel> Parameters => _models.Where(model => model.Kind == VfxModelKind.Parameter).ToArray();

    internal IReadOnlyList<VfxModel> Slots => _models.Where(model => model.Kind == VfxModelKind.Slot).ToArray();

    internal IReadOnlyList<VfxModel> Data => _models.Where(model => model.Kind == VfxModelKind.Data).ToArray();

    internal IReadOnlyList<VfxModel> UnsupportedModels => _models.Where(model => !model.ScriptType.IsProductSupported).ToArray();

    internal IReadOnlyList<VfxFlowEdge> FlowEdges => _flowEdges;

    internal static VfxTypedGraph Build(VfxYamlAsset asset)
    {
        if (asset is null) throw new ArgumentNullException(nameof(asset));

        var models = new List<VfxModel>();
        var modelsByFileId = new Dictionary<long, VfxModel>();
        foreach (VfxYamlDocument document in asset.Documents)
        {
            if (!string.Equals(document.RootType, "MonoBehaviour", StringComparison.Ordinal)) continue;
            VfxModel model = VfxModel.Parse(document);
            models.Add(model);
            modelsByFileId.Add(model.FileId, model);
        }

        long graphFileId = VfxYamlFields.ReadReference(asset.Resource.RawText, "m_Graph");
        if (!modelsByFileId.TryGetValue(graphFileId, out VfxModel? graph) || graph.Kind != VfxModelKind.Graph)
            throw new InvalidDataException("VisualEffectResource.m_Graph must reference one VFXGraph model.");

        ValidateHierarchy(models, modelsByFileId);
        ValidateSlotTopology(models, modelsByFileId);
        ValidateDataAndSubOutputTopology(models, modelsByFileId);
        List<VfxFlowEdge> flowEdges = BuildAndValidateFlowTopology(models, modelsByFileId);
        return new VfxTypedGraph(asset, graph, models, modelsByFileId, flowEdges);
    }

    internal IReadOnlyList<VfxModel> TopologicallySortContexts()
    {
        VfxModel[] contexts = Contexts.ToArray();
        var indegree = contexts.ToDictionary(context => context.FileId, _ => 0);
        var outgoing = contexts.ToDictionary(context => context.FileId, _ => new List<long>());
        foreach ((long Source, long Target) pair in _flowEdges
                     .Where(edge => edge.SourceContextId != edge.TargetContextId)
                     .Select(edge => (edge.SourceContextId, edge.TargetContextId))
                     .Distinct())
        {
            outgoing[pair.Source].Add(pair.Target);
            indegree[pair.Target]++;
        }

        var ready = new SortedSet<(int Order, long FileId)>();
        var orderById = contexts.Select((context, order) => (context.FileId, order))
            .ToDictionary(item => item.FileId, item => item.order);
        foreach (VfxModel context in contexts)
            if (indegree[context.FileId] == 0) ready.Add((orderById[context.FileId], context.FileId));

        var result = new List<VfxModel>(contexts.Length);
        while (ready.Count != 0)
        {
            (int _, long fileId) = ready.Min;
            ready.Remove(ready.Min);
            result.Add(_modelsByFileId[fileId]);
            foreach (long target in outgoing[fileId])
            {
                indegree[target]--;
                if (indegree[target] == 0) ready.Add((orderById[target], target));
            }
        }

        if (result.Count != contexts.Length)
            throw new InvalidDataException("VFX context flow topology contains a cycle.");
        return result.AsReadOnly();
    }

    private static void ValidateHierarchy(
        IReadOnlyList<VfxModel> models,
        IReadOnlyDictionary<long, VfxModel> modelsByFileId)
    {
        foreach (VfxModel model in models)
        {
            if (model.ParentId != 0 && RequiresParentChildReciprocity(model.Kind))
            {
                VfxModel parent = ResolveModel(modelsByFileId, model.ParentId, model.FileId, "parent");
                if (!parent.ChildrenIds.Contains(model.FileId))
                    throw new InvalidDataException($"VFX model '{model.FileId}' is absent from parent '{parent.FileId}' children.");
            }

            foreach (long childId in model.ChildrenIds)
            {
                VfxModel child = ResolveModel(modelsByFileId, childId, model.FileId, "child");
                if (child.ParentId != model.FileId)
                    throw new InvalidDataException($"VFX child '{childId}' does not point back to parent '{model.FileId}'.");
            }
        }
    }

    private static bool RequiresParentChildReciprocity(VfxModelKind kind)
        => kind == VfxModelKind.Context ||
           kind == VfxModelKind.Block ||
           kind == VfxModelKind.Operator ||
           kind == VfxModelKind.Parameter;

    private static void ValidateSlotTopology(
        IReadOnlyList<VfxModel> models,
        IReadOnlyDictionary<long, VfxModel> modelsByFileId)
    {
        foreach (VfxModel owner in models)
        {
            ValidateOwnedSlots(owner, owner.InputSlotIds, 0, modelsByFileId);
            ValidateOwnedSlots(owner, owner.OutputSlotIds, 1, modelsByFileId);
        }

        foreach (VfxModel slot in models.Where(model => model.Kind == VfxModelKind.Slot))
        {
            if (slot.Direction != 0 && slot.Direction != 1)
                throw new InvalidDataException($"VFX slot '{slot.FileId}' has invalid direction '{slot.Direction}'.");
            if (slot.Direction == 0 && slot.LinkedSlotIds.Count > 1)
                throw new InvalidDataException($"VFX input slot '{slot.FileId}' has more than one linked output.");
            if (slot.MasterSlotId != 0)
            {
                VfxModel master = ResolveModel(modelsByFileId, slot.MasterSlotId, slot.FileId, "master slot");
                if (master.Kind != VfxModelKind.Slot)
                    throw new InvalidDataException($"VFX slot '{slot.FileId}' master is not a slot.");
            }
            foreach (long linkedId in slot.LinkedSlotIds)
            {
                VfxModel linked = ResolveModel(modelsByFileId, linkedId, slot.FileId, "linked slot");
                if (linked.Kind != VfxModelKind.Slot)
                    throw new InvalidDataException($"VFX slot '{slot.FileId}' links to a non-slot model.");
                if (!linked.LinkedSlotIds.Contains(slot.FileId))
                    throw new InvalidDataException($"VFX slot link '{slot.FileId}' ↔ '{linkedId}' is not reciprocal.");
                if (linked.Direction == slot.Direction)
                    throw new InvalidDataException($"VFX linked slots '{slot.FileId}' and '{linkedId}' have the same direction.");
            }
        }
    }

    private static void ValidateOwnedSlots(
        VfxModel owner,
        IReadOnlyList<long> slotIds,
        int expectedDirection,
        IReadOnlyDictionary<long, VfxModel> modelsByFileId)
    {
        if (slotIds.Count != slotIds.Distinct().Count())
            throw new InvalidDataException($"VFX model '{owner.FileId}' contains duplicate slot references.");
        foreach (long slotId in slotIds)
        {
            VfxModel slot = ResolveModel(modelsByFileId, slotId, owner.FileId, "slot");
            if (slot.Kind != VfxModelKind.Slot)
                throw new InvalidDataException($"VFX model '{owner.FileId}' references non-slot '{slotId}'.");
            if (slot.OwnerId != owner.FileId)
                throw new InvalidDataException($"VFX slot '{slotId}' owner does not match model '{owner.FileId}'.");
            if (slot.Direction != expectedDirection)
                throw new InvalidDataException($"VFX slot '{slotId}' direction does not match its owner list.");
        }
    }

    private static void ValidateDataAndSubOutputTopology(
        IReadOnlyList<VfxModel> models,
        IReadOnlyDictionary<long, VfxModel> modelsByFileId)
    {
        foreach (VfxModel owner in models)
        {
            if (owner.DataId != 0)
            {
                VfxModel data = ResolveModel(modelsByFileId, owner.DataId, owner.FileId, "data");
                if (data.Kind != VfxModelKind.Data)
                    throw new InvalidDataException($"VFX model '{owner.FileId}' references non-data model '{owner.DataId}'.");
                if (!data.OwnerIds.Contains(owner.FileId))
                    throw new InvalidDataException($"VFX data '{data.FileId}' does not contain owner '{owner.FileId}'.");
            }

            foreach (long ownerId in owner.OwnerIds)
            {
                VfxModel dataOwner = ResolveModel(modelsByFileId, ownerId, owner.FileId, "data owner");
                if (dataOwner.DataId != owner.FileId)
                    throw new InvalidDataException($"VFX data owner '{ownerId}' does not point back to data '{owner.FileId}'.");
            }

            foreach (long subOutputId in owner.SubOutputIds)
            {
                VfxModel subOutput = ResolveModel(modelsByFileId, subOutputId, owner.FileId, "sub-output");
                if (subOutput.Kind != VfxModelKind.External)
                    throw new InvalidDataException($"VFX model '{owner.FileId}' references invalid sub-output '{subOutputId}'.");
            }
        }
    }

    private static List<VfxFlowEdge> BuildAndValidateFlowTopology(
        IReadOnlyList<VfxModel> models,
        IReadOnlyDictionary<long, VfxModel> modelsByFileId)
    {
        VfxModel[] contexts = models.Where(model => model.Kind == VfxModelKind.Context).ToArray();
        var edges = new List<VfxFlowEdge>();
        var edgeKeys = new HashSet<(long Source, int SourceSlot, long Target, int TargetSlot)>();
        foreach (VfxModel source in contexts)
        {
            for (int sourceSlot = 0; sourceSlot < source.OutputFlowSlots.Count; sourceSlot++)
            {
                foreach (VfxSerializedFlowLink link in source.OutputFlowSlots[sourceSlot])
                {
                    VfxModel target = ResolveContext(modelsByFileId, link.ContextFileId, source.FileId);
                    ValidateSlotIndex(link.SlotIndex, target.InputFlowSlots.Count, "input", target.FileId);
                    bool reciprocal = target.InputFlowSlots[link.SlotIndex].Any(candidate =>
                        candidate.ContextFileId == source.FileId && candidate.SlotIndex == sourceSlot);
                    if (!reciprocal)
                        throw new InvalidDataException($"VFX flow edge '{source.FileId}:{sourceSlot}' → '{target.FileId}:{link.SlotIndex}' is not reciprocal.");
                    (long Source, int SourceSlot, long Target, int TargetSlot) key =
                        (source.FileId, sourceSlot, target.FileId, link.SlotIndex);
                    if (edgeKeys.Add(key))
                        edges.Add(new VfxFlowEdge(key.Source, key.SourceSlot, key.Target, key.TargetSlot));
                }
            }
        }

        foreach (VfxModel target in contexts)
        {
            for (int targetSlot = 0; targetSlot < target.InputFlowSlots.Count; targetSlot++)
            {
                foreach (VfxSerializedFlowLink link in target.InputFlowSlots[targetSlot])
                {
                    VfxModel source = ResolveContext(modelsByFileId, link.ContextFileId, target.FileId);
                    ValidateSlotIndex(link.SlotIndex, source.OutputFlowSlots.Count, "output", source.FileId);
                    if (!edgeKeys.Contains((source.FileId, link.SlotIndex, target.FileId, targetSlot)))
                        throw new InvalidDataException($"VFX input flow on '{target.FileId}:{targetSlot}' is not reciprocal.");
                }
            }
        }
        return edges;
    }

    private static void ValidateSlotIndex(int slotIndex, int count, string direction, long contextId)
    {
        if (slotIndex < 0 || slotIndex >= count)
            throw new InvalidDataException($"VFX context '{contextId}' has invalid {direction} flow slot index '{slotIndex}'.");
    }

    private static VfxModel ResolveContext(
        IReadOnlyDictionary<long, VfxModel> modelsByFileId,
        long fileId,
        long ownerId)
    {
        VfxModel context = ResolveModel(modelsByFileId, fileId, ownerId, "flow context");
        if (context.Kind != VfxModelKind.Context)
            throw new InvalidDataException($"VFX flow from '{ownerId}' references non-context '{fileId}'.");
        return context;
    }

    private static VfxModel ResolveModel(
        IReadOnlyDictionary<long, VfxModel> modelsByFileId,
        long fileId,
        long ownerId,
        string role)
    {
        if (!modelsByFileId.TryGetValue(fileId, out VfxModel? model))
            throw new InvalidDataException($"VFX model '{ownerId}' has unresolved {role} fileID '{fileId}'.");
        return model;
    }
}

internal sealed class VfxModel
{
    private VfxModel(
        VfxYamlDocument document,
        VfxScriptType scriptType,
        string serializedName,
        long parentId,
        IReadOnlyList<long> childrenIds,
        IReadOnlyList<long> inputSlotIds,
        IReadOnlyList<long> outputSlotIds,
        long activationSlotId,
        long dataId,
        IReadOnlyList<long> ownerIds,
        IReadOnlyList<long> subOutputIds,
        VfxSlotProperty? slotProperty,
        long ownerId,
        long masterSlotId,
        int? direction,
        IReadOnlyList<long> linkedSlotIds,
        IReadOnlyList<IReadOnlyList<VfxSerializedFlowLink>> inputFlowSlots,
        IReadOnlyList<IReadOnlyList<VfxSerializedFlowLink>> outputFlowSlots)
    {
        Document = document;
        ScriptType = scriptType;
        SerializedName = serializedName;
        ParentId = parentId;
        ChildrenIds = childrenIds;
        InputSlotIds = inputSlotIds;
        OutputSlotIds = outputSlotIds;
        ActivationSlotId = activationSlotId;
        DataId = dataId;
        OwnerIds = ownerIds;
        SubOutputIds = subOutputIds;
        SlotProperty = slotProperty;
        OwnerId = ownerId;
        MasterSlotId = masterSlotId;
        Direction = direction;
        LinkedSlotIds = linkedSlotIds;
        InputFlowSlots = inputFlowSlots;
        OutputFlowSlots = outputFlowSlots;
    }

    internal VfxYamlDocument Document { get; }

    internal long FileId => Document.FileId;

    internal VfxScriptType ScriptType { get; }

    internal VfxModelKind Kind => ScriptType.Kind;

    internal string SerializedName { get; }

    internal long ParentId { get; }

    internal IReadOnlyList<long> ChildrenIds { get; }

    internal IReadOnlyList<long> InputSlotIds { get; }

    internal IReadOnlyList<long> OutputSlotIds { get; }

    internal long ActivationSlotId { get; }

    internal long DataId { get; }

    internal IReadOnlyList<long> OwnerIds { get; }

    internal IReadOnlyList<long> SubOutputIds { get; }

    internal VfxSlotProperty? SlotProperty { get; }

    internal long OwnerId { get; }

    internal long MasterSlotId { get; }

    internal int? Direction { get; }

    internal IReadOnlyList<long> LinkedSlotIds { get; }

    internal IReadOnlyList<IReadOnlyList<VfxSerializedFlowLink>> InputFlowSlots { get; }

    internal IReadOnlyList<IReadOnlyList<VfxSerializedFlowLink>> OutputFlowSlots { get; }

    internal static VfxModel Parse(VfxYamlDocument document)
    {
        string guid = VfxScriptTypeRegistry.NormalizeGuid(VfxYamlFields.ReadRequiredScriptGuid(document.RawText));
        VfxScriptType type = VfxScriptTypeRegistry.TryResolve(guid, out VfxScriptType? known)
            ? known!
            : new VfxScriptType(guid, "MissingScript_" + guid, VfxModelKind.Unknown, false);
        VfxSlotProperty? slotProperty = type.Kind == VfxModelKind.Slot
            ? ParseSlotProperty(document.RawText, type)
            : null;
        return new VfxModel(
            document,
            type,
            VfxYamlFields.ReadString(document.RawText, "m_Name") ?? string.Empty,
            VfxYamlFields.ReadReference(document.RawText, "m_Parent"),
            VfxYamlFields.ReadReferenceList(document.RawText, "m_Children"),
            VfxYamlFields.ReadReferenceList(document.RawText, "m_InputSlots"),
            VfxYamlFields.ReadReferenceList(document.RawText, "m_OutputSlots"),
            VfxYamlFields.ReadReference(document.RawText, "m_ActivationSlot"),
            VfxYamlFields.ReadReference(document.RawText, "m_Data"),
            VfxYamlFields.ReadReferenceList(document.RawText, "m_Owners"),
            VfxYamlFields.ReadReferenceList(document.RawText, "m_SubOutputs"),
            slotProperty,
            VfxYamlFields.ReadNestedReference(document.RawText, "m_MasterData", "m_Owner"),
            VfxYamlFields.ReadReference(document.RawText, "m_MasterSlot"),
            VfxYamlFields.ReadInt32(document.RawText, "m_Direction"),
            VfxYamlFields.ReadReferenceList(document.RawText, "m_LinkedSlots"),
            VfxYamlFields.ReadFlowSlots(document.RawText, "m_InputFlowSlot"),
            VfxYamlFields.ReadFlowSlots(document.RawText, "m_OutputFlowSlot"));
    }

    private static VfxSlotProperty ParseSlotProperty(string rawText, VfxScriptType type)
    {
        string name = VfxYamlFields.ReadDescendantScalar(rawText, "m_Property", "name")
                      ?? throw new InvalidDataException("VFX slot is missing m_Property.name.");
        string serializedType = VfxYamlFields.ReadDescendantScalar(
                                    rawText,
                                    "m_Property",
                                    "m_SerializableType")
                                ?? throw new InvalidDataException("VFX slot is missing its property serialized type.");
        string valueType = VfxYamlFields.ReadDescendantScalar(
                               rawText,
                               "m_MasterData",
                               "m_SerializableType")
                           ?? throw new InvalidDataException("VFX slot is missing its master value serialized type.");
        string rawValue = VfxYamlFields.ReadDescendantScalar(
                              rawText,
                              "m_MasterData",
                              "m_SerializableObject")
                          ?? throw new InvalidDataException("VFX slot is missing its master serialized value.");
        string rawSpace = VfxYamlFields.ReadDescendantScalar(rawText, "m_MasterData", "m_Space")
                          ?? throw new InvalidDataException("VFX slot is missing m_MasterData.m_Space.");
        if (!int.TryParse(rawSpace, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int spaceValue) ||
            (spaceValue != (int)VfxCoordinateSpace.Local &&
             spaceValue != (int)VfxCoordinateSpace.World &&
             spaceValue != (int)VfxCoordinateSpace.None))
            throw new InvalidDataException("VFX slot contains invalid coordinate space '" + rawSpace + "'.");
        return new VfxSlotProperty(
            name,
            VfxSlotValue.NormalizeTypeName(serializedType),
            VfxSlotValue.NormalizeTypeName(valueType),
            (VfxCoordinateSpace)spaceValue,
            VfxSlotValue.Parse(type, serializedType, valueType, rawValue));
    }
}

internal readonly struct VfxFlowEdge
{
    internal VfxFlowEdge(long sourceContextId, int sourceSlotIndex, long targetContextId, int targetSlotIndex)
    {
        SourceContextId = sourceContextId;
        SourceSlotIndex = sourceSlotIndex;
        TargetContextId = targetContextId;
        TargetSlotIndex = targetSlotIndex;
    }

    internal long SourceContextId { get; }

    internal int SourceSlotIndex { get; }

    internal long TargetContextId { get; }

    internal int TargetSlotIndex { get; }
}
