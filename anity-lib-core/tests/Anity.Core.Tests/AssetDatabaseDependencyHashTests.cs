using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>Content and transitive-dependency invalidation coverage for AssetDatabase hashes.</summary>
[Collection(AssetPipelineStateCollection.Name)]
public sealed class AssetDatabaseDependencyHashTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-dependency-hash-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public AssetDatabaseDependencyHashTests()
    {
        Directory.CreateDirectory(_dir);
        EditorApplication.OpenProject(_dir);
    }

    public void Dispose()
    {
        EditorApplication.OpenProject(_originalDirectory);
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact] public void EmptyPath_ReturnsDefaultHash() => Assert.Equal(default, AssetDatabase.GetAssetDependencyHash(string.Empty));
    [Fact] public void ExistingAsset_ReturnsValidHash() { var path = Add("Root.asset", 1); Assert.True(AssetDatabase.GetAssetDependencyHash(path).isValid); }
    [Fact] public void RepeatedQuery_IsStable() { var path = Add("Root.asset", 1, "payload"); Assert.Equal(AssetDatabase.GetAssetDependencyHash(path), AssetDatabase.GetAssetDependencyHash(path)); }
    [Fact] public void SourceContentChange_ChangesHash() { var path = Add("Root.asset", 1, "first"); var before = AssetDatabase.GetAssetDependencyHash(path); Rewrite(path, "second"); Assert.NotEqual(before, AssetDatabase.GetAssetDependencyHash(path)); }
    [Fact] public void MetaContentChange_ChangesHash() { var path = Add("Root.asset", 1); var before = AssetDatabase.GetAssetDependencyHash(path); File.AppendAllText(FullPath(path) + ".meta", "userData: changed\n"); Assert.NotEqual(before, AssetDatabase.GetAssetDependencyHash(path)); }
    [Fact] public void DirectDependencyContentChange_ChangesRootHash() { var dep = Add("Dep.asset", 2, "before"); var root = Add("Root.asset", 1, Ref(dep)); var before = AssetDatabase.GetAssetDependencyHash(root); Rewrite(dep, "after"); Assert.NotEqual(before, AssetDatabase.GetAssetDependencyHash(root)); }
    [Fact] public void TransitiveDependencyContentChange_ChangesRootHash() { var leaf = Add("Leaf.asset", 3, "before"); var middle = Add("Middle.asset", 2, Ref(leaf)); var root = Add("Root.asset", 1, Ref(middle)); var before = AssetDatabase.GetAssetDependencyHash(root); Rewrite(leaf, "after"); Assert.NotEqual(before, AssetDatabase.GetAssetDependencyHash(root)); }
    [Fact] public void UnrelatedAssetChange_DoesNotChangeRootHash() { var root = Add("Root.asset", 1, "root"); var other = Add("Other.asset", 2, "before"); var before = AssetDatabase.GetAssetDependencyHash(root); Rewrite(other, "after"); Assert.Equal(before, AssetDatabase.GetAssetDependencyHash(root)); }
    [Fact] public void CyclicDependencies_ReturnAStableValidHash() { var first = Add("First.asset", 1); var second = Add("Second.asset", 2, Ref(first)); Rewrite(first, Ref(second)); var firstHash = AssetDatabase.GetAssetDependencyHash(first); Assert.True(firstHash.isValid); Assert.Equal(firstHash, AssetDatabase.GetAssetDependencyHash(first)); }
    [Fact] public void MissingAssetPath_ReturnsStableValidHash() { const string path = "Assets/Hash/Missing.asset"; var hash = AssetDatabase.GetAssetDependencyHash(path); Assert.True(hash.isValid); Assert.Equal(hash, AssetDatabase.GetAssetDependencyHash(path)); }

    private string Add(string name, int id, string body = "")
    {
        var path = "Assets/Hash/" + name;
        Directory.CreateDirectory(Path.GetDirectoryName(FullPath(path))!);
        File.WriteAllText(FullPath(path), body);
        File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\nguid: " + GuidFor(id) + "\n");
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private void Rewrite(string path, string body)
    {
        File.WriteAllText(FullPath(path), body);
        AssetDatabase.ImportAsset(path);
    }

    private string FullPath(string path) => Path.Combine(_dir, path);
    private static string GuidFor(int id) => id.ToString("x32");
    private static string Ref(string path) => "ref: { fileID: 11400000, guid: " + AssetDatabase.AssetPathToGUID(path) + ", type: 2 }";
}
