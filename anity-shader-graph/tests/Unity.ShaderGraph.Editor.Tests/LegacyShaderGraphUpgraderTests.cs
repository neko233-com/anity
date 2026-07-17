using System.Text.Json;
using UnityEditor.ShaderGraph.Model;
using UnityEditor.ShaderGraph.Serialization;
using Xunit;

namespace Unity.ShaderGraph.Editor.Tests;

public sealed class LegacyShaderGraphUpgraderTests
{
    [Fact]
    public void Upgrade_ProducesModernGraphDataAndDeterministicObjectIds()
    {
        MultiJsonAsset legacy = MultiJsonAsset.Parse(LegacyGraph());

        MultiJsonAsset first = LegacyShaderGraphUpgrader.Upgrade(legacy);
        MultiJsonAsset second = LegacyShaderGraphUpgrader.Upgrade(legacy);

        Assert.Equal(ShaderGraphSerializationFormat.MultiJson, first.Format);
        Assert.Equal(first.SourceText, second.SourceText);
        Assert.Equal("UnityEditor.ShaderGraph.GraphData", first.Graph.Type);
    }

    [Fact]
    public void Upgrade_ConvertsPropertyNamespaceAndBlackboardReference()
    {
        MultiJsonAsset upgraded = Upgrade();

        ShaderGraphProperty property = Assert.Single(ShaderGraphBlackboard.Create(upgraded).Properties);

        Assert.Equal(ShaderGraphPropertyKind.Vector1, property.Kind);
        Assert.Equal("_Speed", property.ReferenceName);
    }

    [Fact]
    public void Upgrade_GeneratesDefaultReferenceNameWhenLegacyFieldIsEmpty()
    {
        MultiJsonAsset upgraded = Upgrade(propertyDefaultReference: string.Empty, propertyOverrideReference: string.Empty);

        Assert.Equal("_Speed", Assert.Single(ShaderGraphBlackboard.Create(upgraded).Properties).ReferenceName);
    }

    [Fact]
    public void Upgrade_ConvertsNodeAndStandaloneSlots()
    {
        MultiJsonAsset upgraded = Upgrade();
        ShaderGraphTopology topology = ShaderGraphTopology.Create(upgraded);

        Assert.Equal(2, topology.Nodes.Count);
        MultiJsonDocument add = Assert.Single(topology.Nodes.Where(node => node.Type.EndsWith("AddNode", StringComparison.Ordinal)));
        Assert.Equal(3, add.Root.GetProperty("m_Slots").GetArrayLength());
        foreach (JsonElement slot in add.Root.GetProperty("m_Slots").EnumerateArray())
            Assert.True(upgraded.TryResolve(slot.GetProperty("m_Id").GetString()!, out _));
    }

    [Fact]
    public void Upgrade_ConvertsLegacyEdgesToModernNodeReferences()
    {
        ShaderGraphTopology topology = ShaderGraphTopology.Create(Upgrade());

        ShaderGraphEdge edge = Assert.Single(topology.Edges);
        Assert.Equal(0, edge.Output.SlotId);
        Assert.Equal(0, edge.Input.SlotId);
    }

    [Fact]
    public void Upgrade_PropertyNodePointsAtConvertedPropertyObject()
    {
        MultiJsonAsset upgraded = Upgrade();
        MultiJsonDocument propertyNode = ShaderGraphTopology.Create(upgraded).Nodes
            .Single(node => node.Type.EndsWith("PropertyNode", StringComparison.Ordinal));

        string propertyId = propertyNode.Root.GetProperty("m_Property").GetProperty("m_Id").GetString()!;
        Assert.Equal(Assert.Single(ShaderGraphBlackboard.Create(upgraded).Properties).ObjectId, propertyId);
    }

    [Fact]
    public void Upgrade_ConvertsGroupAndNodeMembership()
    {
        MultiJsonAsset upgraded = Upgrade(includeGroup: true);
        MultiJsonDocument add = ShaderGraphTopology.Create(upgraded).Nodes
            .Single(node => node.Type.EndsWith("AddNode", StringComparison.Ordinal));
        string groupId = add.Root.GetProperty("m_Group").GetProperty("m_Id").GetString()!;

        Assert.True(upgraded.TryResolve(groupId, out MultiJsonDocument? group));
        Assert.Equal("UnityEditor.ShaderGraph.GroupData", group!.Type);
    }

    [Fact]
    public void Upgrade_CategoryContainsPropertiesInLegacyOrder()
    {
        ShaderGraphBlackboard blackboard = ShaderGraphBlackboard.Create(Upgrade());

        ShaderGraphCategory category = Assert.Single(blackboard.Categories);
        Assert.Equal(new[] { Assert.Single(blackboard.Properties).ObjectId }, category.ChildObjectIds);
    }

    [Fact]
    public void Upgrade_LeavesNoUnresolvedModernObjectReferences()
    {
        Assert.Empty(Upgrade(includeGroup: true).GetUnresolvedObjectIds());
    }

    [Fact]
    public void Upgrade_NullAndModernAssetsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => LegacyShaderGraphUpgrader.Upgrade(null!));
        MultiJsonAsset modern = MultiJsonAsset.Parse("{\"m_SGVersion\":3,\"m_Type\":\"UnityEditor.ShaderGraph.GraphData\",\"m_ObjectId\":\"graph\",\"m_Nodes\":[],\"m_Edges\":[]}");
        Assert.Throws<ArgumentException>(() => LegacyShaderGraphUpgrader.Upgrade(modern));
    }

    [Fact]
    public void Upgrade_InvalidLegacyGuidIsRejected()
    {
        string invalid = LegacyGraph().Replace(PropertyGuid, "not-a-guid", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => LegacyShaderGraphUpgrader.Upgrade(MultiJsonAsset.Parse(invalid)));
    }

    [Fact]
    public void Upgrade_AllInstalledOfficialLegacySubgraphs_WhenAvailable()
    {
        string? packageRoot = FindInstalledShaderGraphPackage();
        if (packageRoot is null) return;
        string root = Path.Combine(packageRoot, "Samples~");
        string[] fixtures = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".shadersubgraph", StringComparison.OrdinalIgnoreCase))
            .Where(path => MultiJsonAsset.Parse(File.ReadAllText(path)).Format == ShaderGraphSerializationFormat.LegacySingleJson)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(fixtures);
        foreach (string fixture in fixtures)
        {
            MultiJsonAsset upgraded = LegacyShaderGraphUpgrader.Upgrade(MultiJsonAsset.Parse(File.ReadAllText(fixture)));
            Assert.Empty(upgraded.GetUnresolvedObjectIds());
            Assert.True(ShaderGraphTopology.Create(upgraded).TryGetTopologicalOrder(out _), fixture);
            _ = ShaderGraphBlackboard.Create(upgraded);
        }
    }

    private const string PropertyGuid = "11111111-1111-1111-1111-111111111111";
    private const string PropertyNodeGuid = "22222222-2222-2222-2222-222222222222";
    private const string AddNodeGuid = "33333333-3333-3333-3333-333333333333";
    private const string GroupGuid = "44444444-4444-4444-4444-444444444444";

    private static MultiJsonAsset Upgrade(
        string propertyDefaultReference = "_Speed",
        string propertyOverrideReference = "",
        bool includeGroup = false)
        => LegacyShaderGraphUpgrader.Upgrade(MultiJsonAsset.Parse(
            LegacyGraph(propertyDefaultReference, propertyOverrideReference, includeGroup)));

    private static string LegacyGraph(
        string propertyDefaultReference = "_Speed",
        string propertyOverrideReference = "",
        bool includeGroup = false)
    {
        object property = Wrapper("UnityEditor.ShaderGraph.Vector1ShaderProperty", new
        {
            m_Name = "Speed",
            m_GeneratePropertyBlock = true,
            m_Guid = new { m_GuidSerialized = PropertyGuid },
            m_DefaultReferenceName = propertyDefaultReference,
            m_OverrideReferenceName = propertyOverrideReference,
            m_Value = 2.0,
            m_FloatType = 0,
            m_RangeValues = new { x = 0.0, y = 1.0 }
        });
        object propertyNode = Wrapper("UnityEditor.ShaderGraph.PropertyNode", new
        {
            m_GuidSerialized = PropertyNodeGuid,
            m_GroupGuidSerialized = "00000000-0000-0000-0000-000000000000",
            m_Name = "Property",
            m_SerializableSlots = new[] { Slot("UnityEditor.ShaderGraph.Vector1MaterialSlot", 0, 1, 0.0) },
            m_PropertyGuidSerialized = PropertyGuid
        });
        object addNode = Wrapper("UnityEditor.ShaderGraph.AddNode", new
        {
            m_GuidSerialized = AddNodeGuid,
            m_GroupGuidSerialized = includeGroup ? GroupGuid : "00000000-0000-0000-0000-000000000000",
            m_Name = "Add",
            m_SerializableSlots = new[]
            {
                Slot("UnityEditor.ShaderGraph.Vector1MaterialSlot", 0, 0, 0.0),
                Slot("UnityEditor.ShaderGraph.Vector1MaterialSlot", 1, 0, 1.0),
                Slot("UnityEditor.ShaderGraph.Vector1MaterialSlot", 2, 1, 0.0)
            }
        });
        object edge = Wrapper("UnityEditor.Graphing.Edge", new
        {
            m_OutputSlot = new { m_SlotId = 0, m_NodeGUIDSerialized = PropertyNodeGuid },
            m_InputSlot = new { m_SlotId = 0, m_NodeGUIDSerialized = AddNodeGuid }
        });
        var root = new Dictionary<string, object?>
        {
            ["m_SerializedProperties"] = new[] { property },
            ["m_SerializableNodes"] = new[] { propertyNode, addNode },
            ["m_SerializableEdges"] = new[] { edge },
            ["m_Groups"] = includeGroup
                ? new object[] { new { m_GuidSerialized = GroupGuid, m_Title = "Math", m_Position = new { x = 1.0, y = 2.0 } } }
                : Array.Empty<object>(),
            ["m_Path"] = "Patterns"
        };
        return JsonSerializer.Serialize(root);
    }

    private static object Wrapper(string type, object payload)
        => new { typeInfo = new { fullName = type }, JSONnodeData = JsonSerializer.Serialize(payload) };

    private static object Slot(string type, int id, int slotType, double value)
        => Wrapper(type, new { m_Id = id, m_SlotType = slotType, m_Value = value });

    private static string? FindInstalledShaderGraphPackage()
    {
        string? configured = Environment.GetEnvironmentVariable("ANITY_UNITY_SHADERGRAPH_PACKAGE");
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured)) return configured;
        const string hubRoot = "/Applications/Unity/Hub/Editor";
        if (!Directory.Exists(hubRoot)) return null;
        return Directory.EnumerateDirectories(hubRoot, "2022.3.*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(path => path, StringComparer.Ordinal)
            .Select(path => Path.Combine(path, "Unity.app/Contents/Resources/PackageManager/BuiltInPackages/com.unity.shadergraph"))
            .FirstOrDefault(Directory.Exists);
    }
}
