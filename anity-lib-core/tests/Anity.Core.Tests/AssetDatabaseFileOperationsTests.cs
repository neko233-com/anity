using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Xunit;
using EditorAssetImporter = UnityEditor.AssetImporter;

namespace Anity.Core.Tests;

/// <summary>Disk-backed DeleteAsset, MoveAsset and CopyAsset transaction coverage.</summary>
[Collection(AssetPipelineStateCollection.Name)]
public sealed class AssetDatabaseFileOperationsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-file-ops-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public AssetDatabaseFileOperationsTests()
    {
        Directory.CreateDirectory(_dir);
        EditorApplication.OpenProject(_dir);
    }

    public void Dispose()
    {
        EditorApplication.OpenProject(_originalDirectory);
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact] public void DeleteAsset_RemovesDiskAssetAndMeta() { var path = Track("delete.txt", "old"); Assert.True(AssetDatabase.DeleteAsset(path)); Assert.False(File.Exists(FullPath(path))); Assert.False(File.Exists(FullPath(path) + ".meta")); Assert.Null(AssetDatabase.LoadAssetAtPath<TextAsset>(path)); }
    [Fact] public void DeleteAsset_RemovesImporterRegistry() { var path = Track("importer.txt", "old"); Assert.False(EditorAssetImporter.GetAtPath(path).importSettingsMissing); AssetDatabase.DeleteAsset(path); Assert.True(EditorAssetImporter.GetAtPath(path).importSettingsMissing); }
    [Fact] public void DeleteAsset_MissingAssetReturnsFalse() { Assert.False(AssetDatabase.DeleteAsset("Assets/Missing/file.txt")); }
    [Fact] public void DeleteAsset_RemovesUnindexedDiskAsset() { const string path = "Assets/FileOps/unindexed.txt"; Directory.CreateDirectory(Path.GetDirectoryName(FullPath(path))!); File.WriteAllText(FullPath(path), "disk"); File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\n"); Assert.True(AssetDatabase.DeleteAsset(path)); Assert.False(File.Exists(FullPath(path))); Assert.False(File.Exists(FullPath(path) + ".meta")); }
    [Fact] public void DeleteAsset_MemoryOnlyAssetStillDeletesIndex() { var path = "Assets/Virtual/" + Guid.NewGuid().ToString("N") + ".txt"; AssetDatabase.CreateAsset(new TextAsset("memory"), path); Assert.True(AssetDatabase.DeleteAsset(path)); Assert.Null(AssetDatabase.LoadAssetAtPath<TextAsset>(path)); }
    [Fact] public void MoveAsset_MovesDiskAssetAndMeta() { var source = Track("move.txt", "move"); var target = Sibling(source, "moved.txt"); Assert.Equal(target, AssetDatabase.MoveAsset(source, target)); Assert.False(File.Exists(FullPath(source))); Assert.False(File.Exists(FullPath(source) + ".meta")); Assert.True(File.Exists(FullPath(target))); Assert.True(File.Exists(FullPath(target) + ".meta")); Assert.Equal("move", AssetDatabase.LoadAssetAtPath<TextAsset>(target)!.text); }
    [Fact] public void MoveAsset_PreservesAssetGuid() { var source = Track("guid.txt"); var guid = AssetDatabase.AssetPathToGUID(source); var target = Sibling(source, "guid-moved.txt"); AssetDatabase.MoveAsset(source, target); Assert.Equal(guid, AssetDatabase.AssetPathToGUID(target)); }
    [Fact] public void MoveAsset_PreservesImporterIdentityAndUpdatesPath() { var source = Track("importer-move.txt"); var importer = EditorAssetImporter.GetAtPath(source); var target = Sibling(source, "importer-moved.txt"); AssetDatabase.MoveAsset(source, target); Assert.Same(importer, EditorAssetImporter.GetAtPath(target)); Assert.Equal(target, importer.assetPath); }
    [Fact] public void MoveAsset_RefusesExistingDestination() { var source = Track("source.txt"); var target = Track("target.txt"); Assert.Equal(string.Empty, AssetDatabase.MoveAsset(source, target)); Assert.True(File.Exists(FullPath(source))); Assert.Equal("source", AssetDatabase.LoadAssetAtPath<TextAsset>(source)!.text); }
    [Fact] public void CopyAsset_CopiesDiskAssetAndKeepsSource() { var source = Track("copy.txt", "copy"); var target = Sibling(source, "copy-target.txt"); Assert.True(AssetDatabase.CopyAsset(source, target)); Assert.True(File.Exists(FullPath(source))); Assert.True(File.Exists(FullPath(target))); Assert.Equal("copy", AssetDatabase.LoadAssetAtPath<TextAsset>(source)!.text); Assert.Equal("copy", AssetDatabase.LoadAssetAtPath<TextAsset>(target)!.text); }
    [Fact] public void CopyAsset_AssignsNewGuidToMetaAndDatabase() { var source = Track("copy-guid.txt"); var target = Sibling(source, "copy-guid-target.txt"); Assert.True(AssetDatabase.CopyAsset(source, target)); var targetGuid = AssetDatabase.AssetPathToGUID(target); Assert.NotEqual(AssetDatabase.AssetPathToGUID(source), targetGuid); Assert.Contains("guid: " + targetGuid, File.ReadAllText(FullPath(target) + ".meta")); }
    [Fact] public void ImportAsset_UsesUnityMetaGuidForDatabaseLookup() { const string guid = "0123456789abcdef0123456789abcdef"; var path = TrackWithMeta("meta-guid.txt", "guid: " + guid); Assert.Equal(guid, AssetDatabase.AssetPathToGUID(path)); Assert.Equal(path, AssetDatabase.GUIDToAssetPath(guid)); }
    [Fact] public void ImportAsset_NormalizesUppercaseMetaGuid() { const string upperGuid = "ABCDEFABCDEFABCDEFABCDEFABCDEFAB"; var path = TrackWithMeta("upper-guid.txt", "guid: " + upperGuid); Assert.Equal(upperGuid.ToLowerInvariant(), AssetDatabase.AssetPathToGUID(path)); }
    [Fact] public void ImportAsset_RejectsNonUnityMetaGuid() { var path = TrackWithMeta("invalid-guid.txt", "guid: short-guid"); Assert.Matches("^[0-9a-f]{32}$", AssetDatabase.AssetPathToGUID(path)); }
    [Fact] public void SaveSettings_WritesCanonicalUnityGuidWhenMetaHasNone() { var path = TrackWithMeta("write-guid.txt", string.Empty); EditorAssetImporter.GetAtPath(path).SaveSettings(); var guid = AssetDatabase.AssetPathToGUID(path); Assert.Matches("^[0-9a-f]{32}$", guid); Assert.Contains("guid: " + guid, File.ReadAllText(FullPath(path) + ".meta")); }

    private string Track(string name, string content = "source")
    {
        var path = "Assets/FileOps/" + Guid.NewGuid().ToString("N") + "/" + name;
        Directory.CreateDirectory(Path.GetDirectoryName(FullPath(path))!);
        File.WriteAllText(FullPath(path), content);
        File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\nguid: fixture-guid\n");
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private string TrackWithMeta(string name, string metaLine)
    {
        var path = "Assets/FileOps/" + Guid.NewGuid().ToString("N") + "/" + name;
        Directory.CreateDirectory(Path.GetDirectoryName(FullPath(path))!);
        File.WriteAllText(FullPath(path), "source");
        File.WriteAllText(FullPath(path) + ".meta", "fileFormatVersion: 2\n" + metaLine + "\n");
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private static string Sibling(string path, string name) => Path.GetDirectoryName(path)!.Replace('\\', '/') + "/" + name;
    private string FullPath(string path) => Path.Combine(_dir, path);
}
