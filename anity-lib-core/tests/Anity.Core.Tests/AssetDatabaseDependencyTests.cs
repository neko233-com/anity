using System;
using System.IO;
using UnityEditor;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>YAML GUID dependency extraction for direct and recursive AssetDatabase queries.</summary>
[Collection(AssetPipelineStateCollection.Name)]
public sealed class AssetDatabaseDependencyTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-dependencies-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public AssetDatabaseDependencyTests()
    {
        Directory.CreateDirectory(_dir);
        EditorApplication.OpenProject(_dir);
    }

    public void Dispose()
    {
        EditorApplication.OpenProject(_originalDirectory);
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact] public void EmptyPath_ReturnsEmpty() => Assert.Empty(AssetDatabase.GetDependencies(string.Empty, true));
    [Fact] public void DirectQuery_IncludesAssetItself() { var root = Add("Root.asset", 1); Assert.Equal(new[] { root }, AssetDatabase.GetDependencies(root, false)); }
    [Fact] public void DirectQuery_ReturnsSingleGuidReference() { var dep = Add("Dep.asset", 2); var root = Add("Root.asset", 1, Ref(dep)); Assert.Equal(new[] { root, dep }, AssetDatabase.GetDependencies(root, false)); }
    [Fact] public void DirectQuery_ExcludesTransitiveDependency() { var leaf = Add("Leaf.asset", 3); var dep = Add("Dep.asset", 2, Ref(leaf)); var root = Add("Root.asset", 1, Ref(dep)); Assert.Equal(new[] { root, dep }, AssetDatabase.GetDependencies(root, false)); }
    [Fact] public void RecursiveQuery_IncludesTransitiveDependency() { var leaf = Add("Leaf.asset", 3); var dep = Add("Dep.asset", 2, Ref(leaf)); var root = Add("Root.asset", 1, Ref(dep)); Assert.Equal(new[] { root, dep, leaf }, AssetDatabase.GetDependencies(root, true)); }
    [Fact] public void DirectQuery_SortsMultipleDependenciesByPath() { var z = Add("Z.asset", 3); var a = Add("A.asset", 2); var root = Add("Root.asset", 1, Ref(z) + "\n" + Ref(a)); Assert.Equal(new[] { root, a, z }, AssetDatabase.GetDependencies(root, false)); }
    [Fact] public void RecursiveQuery_HandlesCycles() { var first = Add("First.asset", 1); var second = Add("Second.asset", 2, Ref(first)); Rewrite(first, Ref(second)); Assert.Equal(new[] { first, second }, AssetDatabase.GetDependencies(first, true)); }
    [Fact] public void Query_IgnoresMalformedGuid() { var root = Add("Root.asset", 1, "ref: { guid: short }\n"); Assert.Equal(new[] { root }, AssetDatabase.GetDependencies(root, true)); }
    [Fact] public void Query_IgnoresUnknownGuid() { var root = Add("Root.asset", 1, "ref: { guid: 99999999999999999999999999999999 }\n"); Assert.Equal(new[] { root }, AssetDatabase.GetDependencies(root, true)); }
    [Fact] public void Query_ReadsGuidReferenceFromMeta() { var dep = Add("Dep.asset", 2); var root = Add("Root.asset", 1, string.Empty, "external: { guid: " + GuidFor(2) + " }"); Assert.Equal(new[] { root, dep }, AssetDatabase.GetDependencies(root, false)); }

    private string Add(string name, int id, string body = "", string metaSuffix = "")
    {
        var path = "Assets/Deps/" + name;
        Write(path, body);
        Write(path + ".meta", "fileFormatVersion: 2\nguid: " + GuidFor(id) + "\n" + metaSuffix + "\n");
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private void Rewrite(string path, string body)
    {
        Write(path, body);
        AssetDatabase.ImportAsset(path);
    }

    private static string GuidFor(int id) => id.ToString("x32");
    private static string Ref(string path) => "ref: { fileID: 11400000, guid: " + AssetDatabase.AssetPathToGUID(path) + ", type: 2 }";

    private void Write(string path, string content)
    {
        var diskPath = Path.Combine(_dir, path);
        Directory.CreateDirectory(Path.GetDirectoryName(diskPath)!);
        File.WriteAllText(diskPath, content);
    }
}
