using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Xunit;
using EditorAssetImporter = UnityEditor.AssetImporter;

namespace Anity.Core.Tests;

/// <summary>Transactional batch MoveAsset coverage for in-memory and disk-backed project assets.</summary>
[Collection(AssetPipelineStateCollection.Name)]
public sealed class AssetDatabaseBatchMoveTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-batch-move-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public AssetDatabaseBatchMoveTests()
    {
        Directory.CreateDirectory(_dir);
        EditorApplication.OpenProject(_dir);
    }

    public void Dispose()
    {
        EditorApplication.OpenProject(_originalDirectory);
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void MoveAsset_BatchMovesOneVirtualAsset()
    {
        var source = Create("One.asset");
        var destination = Destination("One");

        Assert.True(AssetDatabase.MoveAsset(new[] { source }, destination));
        Assert.True(AssetDatabase.Contains(destination + "/One.asset"));
    }

    [Fact]
    public void MoveAsset_BatchMovesEveryAsset()
    {
        var first = Create("First.asset");
        var second = Create("Second.asset");
        var destination = Destination("Many");

        Assert.True(AssetDatabase.MoveAsset(new[] { first, second }, destination));
        Assert.True(AssetDatabase.Contains(destination + "/First.asset"));
        Assert.True(AssetDatabase.Contains(destination + "/Second.asset"));
    }

    [Fact]
    public void MoveAsset_BatchPreservesFileNames()
    {
        var source = Create("Original.asset");
        var destination = Destination("Names");

        AssetDatabase.MoveAsset(new[] { source }, destination);

        Assert.True(AssetDatabase.Contains(destination + "/Original.asset"));
    }

    [Fact]
    public void MoveAsset_BatchPreservesGuid()
    {
        var source = Create("Guid.asset");
        var guid = AssetDatabase.AssetPathToGUID(source);
        var destination = Destination("Guid");

        Assert.True(AssetDatabase.MoveAsset(new[] { source }, destination));
        Assert.Equal(guid, AssetDatabase.AssetPathToGUID(destination + "/Guid.asset"));
    }

    [Fact]
    public void MoveAsset_BatchPreservesImporterIdentity()
    {
        var source = Create("Importer.asset");
        var importer = EditorAssetImporter.GetAtPath(source);
        var destination = Destination("Importer");

        Assert.True(AssetDatabase.MoveAsset(new[] { source }, destination));
        Assert.Same(importer, EditorAssetImporter.GetAtPath(destination + "/Importer.asset"));
    }

    [Fact]
    public void MoveAsset_BatchPreservesSubAssets()
    {
        var source = Create("Sub.asset");
        var child = new TextAsset("child");
        AssetDatabase.AddObjectToAsset(child, source);
        var destination = Destination("Sub");

        Assert.True(AssetDatabase.MoveAsset(new[] { source }, destination));
        Assert.Equal(destination + "/Sub.asset", AssetDatabase.GetAssetPath(child));
    }

    [Fact]
    public void MoveAsset_BatchRejectsMissingDestination()
    {
        var source = Create("MissingDestination.asset");

        Assert.False(AssetDatabase.MoveAsset(new[] { source }, "Assets/DoesNotExist/" + Guid.NewGuid().ToString("N")));
        Assert.True(AssetDatabase.Contains(source));
    }

    [Fact]
    public void MoveAsset_BatchRejectsMissingSourceWithoutMovingOthers()
    {
        var source = Create("Existing.asset");
        var destination = Destination("MissingSource");

        Assert.False(AssetDatabase.MoveAsset(new[] { source, "Assets/Missing.asset" }, destination));
        Assert.True(AssetDatabase.Contains(source));
        Assert.False(AssetDatabase.Contains(destination + "/Existing.asset"));
    }

    [Fact]
    public void MoveAsset_BatchRejectsDestinationCollisionWithoutMovingOthers()
    {
        var source = Create("Collision.asset");
        var destination = Destination("Collision");
        CreateAt(destination + "/Collision.asset");

        Assert.False(AssetDatabase.MoveAsset(new[] { source }, destination));
        Assert.True(AssetDatabase.Contains(source));
    }

    [Fact]
    public void MoveAsset_BatchRejectsDuplicateSourcePaths()
    {
        var source = Create("Duplicate.asset");
        var destination = Destination("Duplicate");

        Assert.False(AssetDatabase.MoveAsset(new[] { source, source }, destination));
        Assert.True(AssetDatabase.Contains(source));
    }

    [Fact]
    public void MoveAsset_BatchMovesDiskAssetAndMetaFile()
    {
        const string source = "Assets/Disk/Tracked.asset";
        Directory.CreateDirectory(Path.GetDirectoryName(FullPath(source))!);
        File.WriteAllText(FullPath(source), "disk");
        File.WriteAllText(FullPath(source) + ".meta", "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\n");
        AssetDatabase.ImportAsset(source);
        var destination = "Assets/DiskDestination";
        Directory.CreateDirectory(FullPath(destination));

        Assert.True(AssetDatabase.MoveAsset(new[] { source }, destination));
        Assert.False(File.Exists(FullPath(source)));
        Assert.True(File.Exists(FullPath(destination + "/Tracked.asset")));
        Assert.True(File.Exists(FullPath(destination + "/Tracked.asset.meta")));
    }

    private string Create(string name)
    {
        var path = "Assets/Batch/" + Guid.NewGuid().ToString("N") + "/" + name;
        CreateAt(path);
        return path;
    }

    private static void CreateAt(string path) => AssetDatabase.CreateAsset(new TextAsset(path), path);

    private string Destination(string suffix)
    {
        var folder = "Assets/Destinations/" + suffix + Guid.NewGuid().ToString("N");
        var parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        return folder;
    }

    private string FullPath(string path) => Path.Combine(_dir, path);
}
