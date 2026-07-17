using UnityEditor.ShaderGraph.Model;
using UnityEditor.ShaderGraph.Serialization;
using Xunit;

namespace Unity.ShaderGraph.Editor.Tests;

public sealed class ShaderGraphTopologyTests
{
    [Fact]
    public void Create_ResolvesGraphNodesInSerializedOrder()
    {
        ShaderGraphTopology topology = CreateTopology(new[] { "b", "a" });

        Assert.Equal(new[] { "b", "a" }, topology.Nodes.Select(node => node.ObjectId));
        Assert.Equal("UnityEditor.ShaderGraph.AddNode", topology.NodesById["a"].Type);
    }

    [Fact]
    public void Create_ReadsEdgeEndpointsAndSlotIds()
    {
        ShaderGraphTopology topology = CreateTopology(new[] { "a", "b" }, Edge("a", 2, "b", 0));

        ShaderGraphEdge edge = Assert.Single(topology.Edges);
        Assert.Equal("a", edge.Output.NodeObjectId);
        Assert.Equal(2, edge.Output.SlotId);
        Assert.Equal("b", edge.Input.NodeObjectId);
        Assert.Equal(0, edge.Input.SlotId);
    }

    [Fact]
    public void Create_MissingNodeList_ProducesEmptyTopology()
    {
        MultiJsonAsset asset = MultiJsonAsset.Parse(GraphDocument(null, null));

        ShaderGraphTopology topology = ShaderGraphTopology.Create(asset);

        Assert.Empty(topology.Nodes);
        Assert.Empty(topology.Edges);
    }

    [Fact]
    public void Create_NonArrayNodeList_IsRejected()
    {
        MultiJsonAsset asset = MultiJsonAsset.Parse(GraphDocument("{}", "[]"));

        Assert.Throws<InvalidDataException>(() => ShaderGraphTopology.Create(asset));
    }

    [Fact]
    public void Create_MissingReferencedNodeObject_IsRejected()
    {
        MultiJsonAsset asset = MultiJsonAsset.Parse(GraphDocument("[{\"m_Id\":\"missing\"}]", "[]"));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ShaderGraphTopology.Create(asset));
        Assert.Contains("missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_DuplicateNodeReference_IsRejected()
    {
        string assetText = GraphDocument("[{\"m_Id\":\"a\"},{\"m_Id\":\"a\"}]", "[]") + "\n" + Node("a");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => ShaderGraphTopology.Create(MultiJsonAsset.Parse(assetText)));
        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_EdgeEndpointOutsideNodeList_IsRejected()
    {
        MultiJsonAsset asset = MultiJsonAsset.Parse(
            GraphDocument("[{\"m_Id\":\"a\"}]", "[" + Edge("a", 0, "b", 0) + "]") +
            "\n" + Node("a") + "\n" + Node("b"));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ShaderGraphTopology.Create(asset));
        Assert.Contains("not in GraphData.m_Nodes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_MalformedSlotReference_IsRejected()
    {
        MultiJsonAsset asset = MultiJsonAsset.Parse(
            GraphDocument("[{\"m_Id\":\"a\"}]", "[{\"m_OutputSlot\":{},\"m_InputSlot\":{}}]") + "\n" + Node("a"));

        Assert.Throws<InvalidDataException>(() => ShaderGraphTopology.Create(asset));
    }

    [Fact]
    public void Create_NegativeHashedSlotId_IsPreservedLikeUnity14()
    {
        ShaderGraphTopology topology = CreateTopology(new[] { "a", "b" }, Edge("a", -1885748303, "b", 0));

        Assert.Equal(-1885748303, Assert.Single(topology.Edges).Output.SlotId);
    }

    [Fact]
    public void Create_MultipleConnectionsToOneInputSlot_AreRejected()
    {
        MultiJsonAsset asset = BuildAsset(
            new[] { "a", "b", "c" },
            Edge("a", 0, "c", 0) + "," + Edge("b", 0, "c", 0));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => ShaderGraphTopology.Create(asset));
        Assert.Contains("more than one connection", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_LegacyAsset_RequiresUpgradeFirst()
    {
        const string legacy = "{\"m_SerializableNodes\":[],\"m_SerializableEdges\":[]}";

        Assert.Throws<NotSupportedException>(() => ShaderGraphTopology.Create(MultiJsonAsset.Parse(legacy)));
    }

    [Fact]
    public void TryGetTopologicalOrder_PlacesDependenciesBeforeConsumers()
    {
        ShaderGraphTopology topology = CreateTopology(
            new[] { "consumer", "source", "middle" },
            Edge("source", 0, "middle", 0) + "," + Edge("middle", 1, "consumer", 0));

        Assert.True(topology.TryGetTopologicalOrder(out IReadOnlyList<MultiJsonDocument> ordered));
        Assert.Equal(new[] { "source", "middle", "consumer" }, ordered.Select(node => node.ObjectId));
    }

    [Fact]
    public void TryGetTopologicalOrder_PreservesSerializedOrderForIsolatedNodes()
    {
        ShaderGraphTopology topology = CreateTopology(new[] { "c", "a", "b" });

        Assert.True(topology.TryGetTopologicalOrder(out IReadOnlyList<MultiJsonDocument> ordered));
        Assert.Equal(new[] { "c", "a", "b" }, ordered.Select(node => node.ObjectId));
    }

    [Fact]
    public void TryGetTopologicalOrder_CycleReturnsFalseWithoutInventingNodes()
    {
        ShaderGraphTopology topology = CreateTopology(
            new[] { "a", "b" },
            Edge("a", 0, "b", 0) + "," + Edge("b", 0, "a", 0));

        Assert.False(topology.TryGetTopologicalOrder(out IReadOnlyList<MultiJsonDocument> ordered));
        Assert.Empty(ordered);
    }

    [Fact]
    public void TryGetTopologicalOrder_DiamondGraphIncludesEveryNodeOnce()
    {
        ShaderGraphTopology topology = CreateTopology(
            new[] { "root", "left", "right", "output" },
            Edge("root", 0, "left", 0) + "," +
            Edge("root", 0, "right", 0) + "," +
            Edge("left", 0, "output", 0) + "," +
            Edge("right", 0, "output", 1));

        Assert.True(topology.TryGetTopologicalOrder(out IReadOnlyList<MultiJsonDocument> ordered));
        Assert.Equal(4, ordered.Select(node => node.ObjectId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("root", ordered[0].ObjectId);
        Assert.Equal("output", ordered[^1].ObjectId);
    }

    private static ShaderGraphTopology CreateTopology(string[] nodes, string? edges = null)
        => ShaderGraphTopology.Create(BuildAsset(nodes, edges));

    private static MultiJsonAsset BuildAsset(string[] nodes, string? edges)
    {
        string nodeReferences = string.Join(",", nodes.Select(id => $"{{\"m_Id\":\"{id}\"}}"));
        string source = GraphDocument("[" + nodeReferences + "]", "[" + (edges ?? string.Empty) + "]");
        foreach (string node in nodes) source += "\n" + Node(node);
        return MultiJsonAsset.Parse(source);
    }

    private static string GraphDocument(string? nodes, string? edges)
    {
        string fields = string.Empty;
        if (nodes is not null) fields += ",\"m_Nodes\":" + nodes;
        if (edges is not null) fields += ",\"m_Edges\":" + edges;
        return "{\"m_SGVersion\":3,\"m_Type\":\"UnityEditor.ShaderGraph.GraphData\",\"m_ObjectId\":\"graph\"" + fields + "}";
    }

    private static string Node(string id)
        => $"{{\"m_SGVersion\":0,\"m_Type\":\"UnityEditor.ShaderGraph.AddNode\",\"m_ObjectId\":\"{id}\"}}";

    private static string Edge(string outputNode, int outputSlot, string inputNode, int inputSlot)
        => $"{{\"m_OutputSlot\":{{\"m_Node\":{{\"m_Id\":\"{outputNode}\"}},\"m_SlotId\":{outputSlot}}},\"m_InputSlot\":{{\"m_Node\":{{\"m_Id\":\"{inputNode}\"}},\"m_SlotId\":{inputSlot}}}}}";
}
