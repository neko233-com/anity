using System.Collections.ObjectModel;
using System.Text.Json;
using UnityEditor.ShaderGraph.Serialization;

namespace UnityEditor.ShaderGraph.Model;

internal sealed class ShaderGraphTopology
{
    private readonly ReadOnlyCollection<MultiJsonDocument> _nodes;
    private readonly ReadOnlyCollection<ShaderGraphEdge> _edges;
    private readonly ReadOnlyDictionary<string, MultiJsonDocument> _nodesById;

    private ShaderGraphTopology(
        List<MultiJsonDocument> nodes,
        List<ShaderGraphEdge> edges,
        Dictionary<string, MultiJsonDocument> nodesById)
    {
        _nodes = nodes.AsReadOnly();
        _edges = edges.AsReadOnly();
        _nodesById = new ReadOnlyDictionary<string, MultiJsonDocument>(nodesById);
    }

    internal IReadOnlyList<MultiJsonDocument> Nodes => _nodes;

    internal IReadOnlyList<ShaderGraphEdge> Edges => _edges;

    internal IReadOnlyDictionary<string, MultiJsonDocument> NodesById => _nodesById;

    internal static ShaderGraphTopology Create(MultiJsonAsset asset)
    {
        if (asset is null) throw new ArgumentNullException(nameof(asset));
        if (asset.Format != ShaderGraphSerializationFormat.MultiJson)
            throw new NotSupportedException("Legacy Shader Graph topology must be upgraded before modern graph construction.");

        var nodes = new List<MultiJsonDocument>();
        var nodesById = new Dictionary<string, MultiJsonDocument>(StringComparer.Ordinal);
        if (asset.Graph.Root.TryGetProperty("m_Nodes", out JsonElement nodeReferences))
        {
            if (nodeReferences.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("GraphData.m_Nodes must be an array.");
            foreach (JsonElement reference in nodeReferences.EnumerateArray())
            {
                string objectId = ReadObjectReference(reference, "GraphData.m_Nodes");
                if (!asset.TryResolve(objectId, out MultiJsonDocument? node) || node is null)
                    throw new InvalidDataException($"GraphData.m_Nodes references missing object '{objectId}'.");
                if (!nodesById.TryAdd(objectId, node))
                    throw new InvalidDataException($"GraphData.m_Nodes contains duplicate object '{objectId}'.");
                nodes.Add(node);
            }
        }

        var edges = new List<ShaderGraphEdge>();
        var occupiedInputs = new HashSet<InputSlotIdentity>();
        if (asset.Graph.Root.TryGetProperty("m_Edges", out JsonElement serializedEdges))
        {
            if (serializedEdges.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("GraphData.m_Edges must be an array.");
            foreach (JsonElement serializedEdge in serializedEdges.EnumerateArray())
            {
                ShaderGraphSlotReference output = ReadSlotReference(serializedEdge, "m_OutputSlot");
                ShaderGraphSlotReference input = ReadSlotReference(serializedEdge, "m_InputSlot");
                EnsureNodeExists(nodesById, output.NodeObjectId, "output");
                EnsureNodeExists(nodesById, input.NodeObjectId, "input");
                if (!occupiedInputs.Add(new InputSlotIdentity(input.NodeObjectId, input.SlotId)))
                {
                    throw new InvalidDataException(
                        $"Input slot '{input.NodeObjectId}:{input.SlotId}' has more than one connection.");
                }
                edges.Add(new ShaderGraphEdge(output, input));
            }
        }

        return new ShaderGraphTopology(nodes, edges, nodesById);
    }

    internal bool TryGetTopologicalOrder(out IReadOnlyList<MultiJsonDocument> orderedNodes)
    {
        var incoming = _nodesById.Keys.ToDictionary(key => key, _ => 0, StringComparer.Ordinal);
        var outgoing = _nodesById.Keys.ToDictionary(
            key => key,
            _ => new List<string>(),
            StringComparer.Ordinal);
        foreach (ShaderGraphEdge edge in _edges)
        {
            incoming[edge.Input.NodeObjectId]++;
            outgoing[edge.Output.NodeObjectId].Add(edge.Input.NodeObjectId);
        }

        var ready = new Queue<string>(_nodes
            .Where(node => incoming[node.ObjectId] == 0)
            .Select(node => node.ObjectId));
        var result = new List<MultiJsonDocument>(_nodes.Count);
        while (ready.Count > 0)
        {
            string objectId = ready.Dequeue();
            result.Add(_nodesById[objectId]);
            foreach (string dependent in outgoing[objectId])
            {
                incoming[dependent]--;
                if (incoming[dependent] == 0) ready.Enqueue(dependent);
            }
        }

        orderedNodes = result.AsReadOnly();
        return result.Count == _nodes.Count;
    }

    private static ShaderGraphSlotReference ReadSlotReference(JsonElement edge, string propertyName)
    {
        if (edge.ValueKind != JsonValueKind.Object ||
            !edge.TryGetProperty(propertyName, out JsonElement slot) ||
            slot.ValueKind != JsonValueKind.Object ||
            !slot.TryGetProperty("m_Node", out JsonElement nodeReference) ||
            !slot.TryGetProperty("m_SlotId", out JsonElement slotIdValue) ||
            !slotIdValue.TryGetInt32(out int slotId))
        {
            throw new InvalidDataException($"Shader Graph edge has an invalid {propertyName}.");
        }

        return new ShaderGraphSlotReference(ReadObjectReference(nodeReference, propertyName + ".m_Node"), slotId);
    }

    private static string ReadObjectReference(JsonElement reference, string location)
    {
        if (reference.ValueKind != JsonValueKind.Object ||
            !reference.TryGetProperty("m_Id", out JsonElement objectId) ||
            objectId.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(objectId.GetString()))
        {
            throw new InvalidDataException(location + " requires a non-empty string m_Id.");
        }
        return objectId.GetString()!;
    }

    private static void EnsureNodeExists(
        IReadOnlyDictionary<string, MultiJsonDocument> nodesById,
        string objectId,
        string endpoint)
    {
        if (!nodesById.ContainsKey(objectId))
            throw new InvalidDataException($"Shader Graph edge {endpoint} node '{objectId}' is not in GraphData.m_Nodes.");
    }

    private sealed class InputSlotIdentity : IEquatable<InputSlotIdentity>
    {
        internal InputSlotIdentity(string nodeObjectId, int slotId)
        {
            NodeObjectId = nodeObjectId;
            SlotId = slotId;
        }

        private string NodeObjectId { get; }

        private int SlotId { get; }

        public bool Equals(InputSlotIdentity? other)
            => other is not null && SlotId == other.SlotId &&
               string.Equals(NodeObjectId, other.NodeObjectId, StringComparison.Ordinal);

        public override bool Equals(object? obj) => Equals(obj as InputSlotIdentity);

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(NodeObjectId) * 397) ^ SlotId;
            }
        }
    }
}

internal sealed class ShaderGraphSlotReference
{
    internal ShaderGraphSlotReference(string nodeObjectId, int slotId)
    {
        NodeObjectId = nodeObjectId;
        SlotId = slotId;
    }

    internal string NodeObjectId { get; }

    internal int SlotId { get; }
}

internal sealed class ShaderGraphEdge
{
    internal ShaderGraphEdge(ShaderGraphSlotReference output, ShaderGraphSlotReference input)
    {
        Output = output;
        Input = input;
    }

    internal ShaderGraphSlotReference Output { get; }

    internal ShaderGraphSlotReference Input { get; }
}
