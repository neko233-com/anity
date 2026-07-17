using UnityEditor.ShaderGraph.Serialization;
using UnityEditor.ShaderGraph.Model;
using UnityEditor.ShaderGraph.Generation;
using Xunit;

namespace Unity.ShaderGraph.Editor.Tests;

public sealed class MultiJsonAssetTests
{
    [Fact]
    public void Parse_SingleGraphData_ReadsIdentityAndVersion()
    {
        MultiJsonAsset asset = MultiJsonAsset.Parse(Graph("graph", 3));

        Assert.Single(asset.Documents);
        Assert.Equal("graph", asset.Graph.ObjectId);
        Assert.Equal(3, asset.Graph.ShaderGraphVersion);
        Assert.Equal(ShaderGraphSerializationFormat.MultiJson, asset.Format);
    }

    [Fact]
    public void Parse_MultipleObjects_PreservesDocumentOrder()
    {
        MultiJsonAsset asset = MultiJsonAsset.Parse(Graph("graph") + "\n\n" + Node("node-b") + "\n" + Node("node-a"));

        Assert.Equal(new[] { "graph", "node-b", "node-a" }, asset.Documents.Select(document => document.ObjectId));
    }

    [Fact]
    public void Parse_BomAndWhitespace_AreAcceptedWithoutChangingSource()
    {
        string source = "\uFEFF \r\n" + Graph("graph") + "\r\n\t";

        MultiJsonAsset asset = MultiJsonAsset.Parse(source);

        Assert.Equal(source, asset.SourceText);
        Assert.Equal(Graph("graph"), asset.Graph.RawText);
    }

    [Fact]
    public void Parse_BracesAndEscapedQuotesInsideStrings_DoNotSplitObjects()
    {
        string node = "{\"m_SGVersion\":0,\"m_Type\":\"UnityEditor.ShaderGraph.CustomFunctionNode\",\"m_ObjectId\":\"node\",\"m_Name\":\"say \\\"{ok}\\\"\"}";

        MultiJsonAsset asset = MultiJsonAsset.Parse(Graph("graph") + "\n" + node);

        Assert.Equal("say \"{ok}\"", asset.Documents[1].Root.GetProperty("m_Name").GetString());
    }

    [Fact]
    public void ObjectsById_UsesOrdinalIdentityAndResolvesObjects()
    {
        MultiJsonAsset asset = MultiJsonAsset.Parse(Graph("Graph") + "\n" + Node("graph"));

        Assert.Equal(2, asset.ObjectsById.Count);
        Assert.True(asset.TryResolve("Graph", out MultiJsonDocument? graph));
        Assert.Equal("UnityEditor.ShaderGraph.GraphData", graph!.Type);
        Assert.False(asset.TryResolve("GRAPH", out _));
    }

    [Fact]
    public void GetUnresolvedObjectIds_ReturnsSortedDistinctLocalReferences()
    {
        string graph = "{\"m_SGVersion\":3,\"m_Type\":\"UnityEditor.ShaderGraph.GraphData\",\"m_ObjectId\":\"graph\",\"m_Nodes\":[{\"m_Id\":\"z\"},{\"m_Id\":\"a\"},{\"m_Id\":\"z\"}]}";
        MultiJsonAsset asset = MultiJsonAsset.Parse(graph + "\n" + Node("a"));

        Assert.Equal(new[] { "z" }, asset.GetUnresolvedObjectIds());
    }

    [Fact]
    public void GetUnresolvedObjectIds_IgnoresResolvedAndEmptyReferences()
    {
        string graph = "{\"m_SGVersion\":3,\"m_Type\":\"UnityEditor.ShaderGraph.GraphData\",\"m_ObjectId\":\"graph\",\"m_Nodes\":[{\"m_Id\":\"node\"},{\"m_Id\":\"\"},{\"m_Id\":null}]}";
        MultiJsonAsset asset = MultiJsonAsset.Parse(graph + "\n" + Node("node"));

        Assert.Empty(asset.GetUnresolvedObjectIds());
    }

    [Fact]
    public void Parse_DuplicateObjectId_IsRejected()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => MultiJsonAsset.Parse(Graph("same") + "\n" + Node("same")));

        Assert.Contains("Duplicate", exception.Message, StringComparison.Ordinal);
        Assert.Contains("same", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_MissingType_IsRejected()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => MultiJsonAsset.Parse("{\"m_SGVersion\":3,\"m_ObjectId\":\"graph\"}"));

        Assert.Contains("m_Type", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_MissingObjectId_IsRejected()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => MultiJsonAsset.Parse("{\"m_SGVersion\":3,\"m_Type\":\"UnityEditor.ShaderGraph.GraphData\"}"));

        Assert.Contains("m_ObjectId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_NoGraphData_IsRejected()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => MultiJsonAsset.Parse(Node("node")));

        Assert.Contains("GraphData", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_LegacySingleJsonGraph_IsAcceptedForUnityUpgradeCompatibility()
    {
        const string legacy = "{\"m_SerializedProperties\":[],\"m_SerializableNodes\":[],\"m_SerializableEdges\":[],\"m_Path\":\"Patterns\"}";

        MultiJsonAsset asset = MultiJsonAsset.Parse(legacy);

        Assert.Equal(ShaderGraphSerializationFormat.LegacySingleJson, asset.Format);
        Assert.Equal("UnityEditor.ShaderGraph.LegacyGraphData", asset.Graph.Type);
        Assert.Equal(legacy, asset.SourceText);
    }

    [Fact]
    public void Parse_EmptyOrWhitespace_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => MultiJsonAsset.Parse(" \r\n\t"));
    }

    [Fact]
    public void Parse_TrailingNonJsonData_IsRejectedWithOffset()
    {
        string graph = Graph("graph");
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => MultiJsonAsset.Parse(graph + " nope"));

        Assert.Contains((graph.Length + 1).ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_UnterminatedObject_IsRejected()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => MultiJsonAsset.Parse(Graph("graph")[..^1]));

        Assert.Contains("Unterminated", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Null_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => MultiJsonAsset.Parse(null!));
    }

    [Fact]
    public void Parse_OfficialStyleNestedDocuments_RetainsJsonKinds()
    {
        string graph = "{\"m_SGVersion\":3,\"m_Type\":\"UnityEditor.ShaderGraph.GraphData\",\"m_ObjectId\":\"graph\",\"m_Properties\":[],\"m_Keywords\":[],\"m_Nodes\":[{\"m_Id\":\"node\"}]}";
        string node = "{\"m_SGVersion\":0,\"m_Type\":\"UnityEditor.ShaderGraph.Vector1Node\",\"m_ObjectId\":\"node\",\"m_Value\":1.25,\"m_DrawState\":{\"m_Expanded\":true}}";

        MultiJsonAsset asset = MultiJsonAsset.Parse(graph + "\n\n" + node);

        Assert.Equal(1.25, asset.Documents[1].Root.GetProperty("m_Value").GetDouble());
        Assert.True(asset.Documents[1].Root.GetProperty("m_DrawState").GetProperty("m_Expanded").GetBoolean());
        Assert.Empty(asset.GetUnresolvedObjectIds());
    }

    [Fact]
    public void Parse_InstalledOfficialShaderGraphSamples_WhenAvailable()
    {
        string? packageRoot = FindInstalledShaderGraphPackage();
        if (packageRoot is null) return;

        string[] roots = { Path.Combine(packageRoot, "Samples~"), Path.Combine(packageRoot, "ShaderGraphLibrary") };
        string[] fixtures = roots.Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            .Where(path => path.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".shadersubgraph", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(fixtures);
        foreach (string fixture in fixtures)
        {
            MultiJsonAsset asset = MultiJsonAsset.Parse(File.ReadAllText(fixture));
            Assert.NotEmpty(asset.Documents);
            Assert.NotEmpty(asset.Graph.ObjectId);
            IReadOnlyList<string> unresolved = asset.GetUnresolvedObjectIds();
            Assert.True(
                unresolved.Count == 0,
                $"{fixture} contains unresolved local object ids: {string.Join(", ", unresolved.Take(8))}");
            if (asset.Format == ShaderGraphSerializationFormat.MultiJson)
            {
                ShaderGraphTopology topology = ShaderGraphTopology.Create(asset);
                ShaderGraphBlackboard blackboard = ShaderGraphBlackboard.Create(asset);
                ShaderGraphTargetSet targets = ShaderGraphTargetSet.Create(asset);
                ShaderGraphCustomFunctionSet customFunctions = ShaderGraphCustomFunctionSet.Create(asset);
                _ = ShaderKeywordPragmaGenerator.Generate(blackboard);
                Assert.True(
                    topology.TryGetTopologicalOrder(out _),
                    fixture + " contains a cycle in its serialized node graph.");
                Assert.NotNull(blackboard);
                Assert.DoesNotContain(targets.Targets, target => target.Kind == ShaderGraphTargetKind.Unknown);
                Assert.DoesNotContain(targets.Targets, target => target.SubTarget.Kind == ShaderGraphSubTargetKind.Unknown);
                Assert.All(
                    customFunctions.Functions.Where(function => function.IsConfigured),
                    function => Assert.NotEmpty(function.Outputs));
            }
        }
    }

    private static string Graph(string id, int version = 3)
        => $"{{\"m_SGVersion\":{version},\"m_Type\":\"UnityEditor.ShaderGraph.GraphData\",\"m_ObjectId\":\"{id}\",\"m_Nodes\":[]}}";

    private static string Node(string id)
        => $"{{\"m_SGVersion\":0,\"m_Type\":\"UnityEditor.ShaderGraph.AddNode\",\"m_ObjectId\":\"{id}\"}}";

    private static string? FindInstalledShaderGraphPackage()
    {
        string? configured = Environment.GetEnvironmentVariable("ANITY_UNITY_SHADERGRAPH_PACKAGE");
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured)) return configured;

        const string hubRoot = "/Applications/Unity/Hub/Editor";
        if (!Directory.Exists(hubRoot)) return null;
        return Directory.EnumerateDirectories(hubRoot, "2022.3.*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(path => path, StringComparer.Ordinal)
            .Select(path => Path.Combine(
                path,
                "Unity.app/Contents/Resources/PackageManager/BuiltInPackages/com.unity.shadergraph"))
            .FirstOrDefault(Directory.Exists);
    }
}
