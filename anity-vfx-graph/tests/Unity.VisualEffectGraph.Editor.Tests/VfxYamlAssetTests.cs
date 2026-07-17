using UnityEditor.VFX.Serialization;
using UnityEditor.VFX.Model;
using Xunit;

namespace Unity.VisualEffectGraph.Editor.Tests;

public sealed class VfxYamlAssetTests
{
    [Fact]
    public void Parse_ReadsPreambleAndVisualEffectResource()
    {
        string source = Preamble + Resource(9, 1);

        VfxYamlAsset asset = VfxYamlAsset.Parse(source);

        Assert.Equal(Preamble, asset.Preamble);
        Assert.Equal(9, asset.Resource.FileId);
        Assert.Equal(2058629511, asset.Resource.ClassId);
        Assert.Equal("VisualEffectResource", asset.Resource.RootType);
    }

    [Fact]
    public void Parse_MultipleDocuments_PreservesSerializedOrder()
    {
        VfxYamlAsset asset = VfxYamlAsset.Parse(Preamble + MonoBehaviour(5) + Resource(9, 5));

        Assert.Equal(new long[] { 5, 9 }, asset.Documents.Select(document => document.FileId));
    }

    [Fact]
    public void Parse_PreservesSourceAndPerDocumentRawText()
    {
        string source = Preamble + MonoBehaviour(5) + Resource(9, 5);

        VfxYamlAsset asset = VfxYamlAsset.Parse(source);

        Assert.Equal(source, asset.SourceText);
        Assert.Equal(MonoBehaviour(5), asset.Documents[0].RawText);
        Assert.Equal(Resource(9, 5), asset.Documents[1].RawText);
    }

    [Fact]
    public void Parse_IndexesSignedAndLargeFileIds()
    {
        string source = Preamble + MonoBehaviour(-7) + Resource(8926484042661614527, -7);

        VfxYamlAsset asset = VfxYamlAsset.Parse(source);

        Assert.True(asset.TryResolve(-7, out VfxYamlDocument? document));
        Assert.Equal("MonoBehaviour", document!.RootType);
        Assert.True(asset.TryResolve(8926484042661614527, out _));
    }

    [Fact]
    public void Parse_DuplicateFileId_IsRejected()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => VfxYamlAsset.Parse(Preamble + MonoBehaviour(5) + Resource(5, 5)));

        Assert.Contains("Duplicate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_MissingYamlDocuments_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => VfxYamlAsset.Parse(Preamble));
    }

    [Fact]
    public void Parse_MissingVisualEffectResource_IsRejected()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => VfxYamlAsset.Parse(Preamble + MonoBehaviour(5)));

        Assert.Contains("VisualEffectResource", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Null_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => VfxYamlAsset.Parse(null!));
    }

    [Fact]
    public void Parse_RootTypeMustStartImmediatelyAfterHeader()
    {
        string malformed = Preamble + "--- !u!2058629511 &9\n  VisualEffectResource:\n";

        Assert.Throws<InvalidDataException>(() => VfxYamlAsset.Parse(malformed));
    }

    [Fact]
    public void LocalReferences_IncludeInlineFileIdWithoutGuid()
    {
        VfxYamlAsset asset = VfxYamlAsset.Parse(Preamble + MonoBehaviour(5) + Resource(9, 5));

        Assert.Contains(5, asset.Resource.LocalFileIds);
        Assert.Empty(asset.GetUnresolvedLocalFileIds());
    }

    [Fact]
    public void LocalReferences_IgnoreNullFileId()
    {
        string source = Preamble + MonoBehaviour(5) + ResourceBody(9, "  m_Graph: {fileID: 0}\n");

        Assert.Empty(VfxYamlAsset.Parse(source).GetUnresolvedLocalFileIds());
    }

    [Fact]
    public void LocalReferences_IgnoreExternalGuidReferences()
    {
        string source = Preamble + ResourceBody(
            9,
            "  m_Script: {fileID: 11500000, guid: d01270efd3285ea4a9d6c555cb0a8027, type: 3}\n");

        VfxYamlAsset asset = VfxYamlAsset.Parse(source);

        Assert.DoesNotContain(11500000, asset.Resource.LocalFileIds);
        Assert.Empty(asset.GetUnresolvedLocalFileIds());
    }

    [Fact]
    public void GetUnresolvedLocalFileIds_ReturnsSortedDistinctIds()
    {
        string source = Preamble + ResourceBody(
            9,
            "  m_A: {fileID: 7}\n  m_B: {fileID: -3}\n  m_C: {fileID: 7}\n");

        Assert.Equal(new long[] { -3, 7 }, VfxYamlAsset.Parse(source).GetUnresolvedLocalFileIds());
    }

    [Fact]
    public void Parse_ClassIdAndRootTypeRemainIndependent()
    {
        string source = Preamble + "--- !u!114 &5\nMonoBehaviour:\n  m_Name: Graph\n" + Resource(9, 5);

        VfxYamlAsset asset = VfxYamlAsset.Parse(source);

        Assert.Equal(114, asset.Documents[0].ClassId);
        Assert.Equal("MonoBehaviour", asset.Documents[0].RootType);
    }

    [Fact]
    public void FoldedScalar_JoinsUnitySerializableTypeContinuation()
    {
        const string source = "MonoBehaviour:\n  m_customType:\n" +
                              "    m_SerializableType: Tests.Probe, Assembly-CSharp, Version=0.0.0.0,\n" +
                              "      Culture=neutral, PublicKeyToken=null\n" +
                              "  m_Name: Callback\n";

        string? value = VfxYamlFields.ReadDescendantFoldedScalar(
            source, "m_customType", "m_SerializableType");

        Assert.Equal(
            "Tests.Probe, Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
            value);
    }

    [Fact]
    public void Parse_InstalledOfficialVfxTemplatesAndSamples_WhenAvailable()
    {
        string? packageRoot = FindInstalledVfxPackage();
        if (packageRoot is null) return;

        string[] roots = { Path.Combine(packageRoot, "Samples~"), Path.Combine(packageRoot, "Editor", "Templates") };
        string[] fixtures = roots.Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*.vfx", SearchOption.AllDirectories))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(fixtures);
        foreach (string fixture in fixtures)
        {
            VfxYamlAsset asset = VfxYamlAsset.Parse(File.ReadAllText(fixture));
            Assert.NotEmpty(asset.Documents);
            Assert.Empty(asset.GetUnresolvedLocalFileIds());
            VfxTypedGraph graph = VfxTypedGraph.Build(asset);
            _ = VfxContextSchema.Create(graph);
            Assert.NotEmpty(graph.Models);
            Assert.DoesNotContain(graph.Models, model => model.Kind == VfxModelKind.Unknown);
            Assert.Equal(graph.Contexts.Count, graph.TopologicallySortContexts().Count);
        }
    }

    private const string Preamble = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";

    private static string MonoBehaviour(long fileId)
        => $"--- !u!114 &{fileId}\nMonoBehaviour:\n  m_Name: Graph\n";

    private static string Resource(long fileId, long graphFileId)
        => ResourceBody(fileId, $"  m_Graph: {{fileID: {graphFileId}}}\n");

    private static string ResourceBody(long fileId, string body)
        => $"--- !u!2058629511 &{fileId}\nVisualEffectResource:\n" + body;

    private static string? FindInstalledVfxPackage()
    {
        string? configured = Environment.GetEnvironmentVariable("ANITY_UNITY_VFXGRAPH_PACKAGE");
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured)) return configured;

        const string hubRoot = "/Applications/Unity/Hub/Editor";
        if (!Directory.Exists(hubRoot)) return null;
        return Directory.EnumerateDirectories(hubRoot, "2022.3.*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(path => path, StringComparer.Ordinal)
            .Select(path => Path.Combine(
                path,
                "Unity.app/Contents/Resources/PackageManager/BuiltInPackages/com.unity.visualeffectgraph"))
            .FirstOrDefault(Directory.Exists);
    }
}
