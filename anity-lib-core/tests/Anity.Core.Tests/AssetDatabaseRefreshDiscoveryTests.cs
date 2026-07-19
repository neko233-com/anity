using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>Refresh discovers external project Assets changes instead of only refreshing known memory entries.</summary>
[Collection(AssetPipelineStateCollection.Name)]
public sealed class AssetDatabaseRefreshDiscoveryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-refresh-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public AssetDatabaseRefreshDiscoveryTests()
    {
        Directory.CreateDirectory(_dir);
        EditorApplication.OpenProject(_dir);
    }

    public void Dispose()
    {
        EditorApplication.OpenProject(_originalDirectory);
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact] public void Refresh_DiscoversExternalTextAsset() { var path = WriteText("discover.txt", "discovered"); AssetDatabase.Refresh(); Assert.Equal("discovered", AssetDatabase.LoadAssetAtPath<TextAsset>(path)!.text); }
    [Fact] public void Refresh_DiscoversExternalTextureAsset() { var path = WritePng("discover.png"); AssetDatabase.Refresh(); Assert.IsType<Texture2D>(AssetDatabase.LoadAssetAtPath<Texture2D>(path)); }
    [Fact] public void Refresh_UsesExternalMetaGuid() { const string guid = "0123456789abcdef0123456789abcdef"; var path = WriteText("guid.txt", "x", "guid: " + guid); AssetDatabase.Refresh(); Assert.Equal(guid, AssetDatabase.AssetPathToGUID(path)); }
    [Fact] public void Refresh_IgnoresMetaOnlyFile() { WriteRaw("Assets/Refresh/orphan.txt.meta", "fileFormatVersion: 2\n"); AssetDatabase.Refresh(); Assert.Null(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Refresh/orphan.txt")); }
    [Fact] public void Refresh_UpdatesTrackedExternalFile() { var path = WriteText("update.txt", "old"); AssetDatabase.Refresh(); File.WriteAllText(FullPath(path), "new"); AssetDatabase.Refresh(); Assert.Equal("new", AssetDatabase.LoadAssetAtPath<TextAsset>(path)!.text); }
    [Fact] public void Refresh_RemovesDeletedTrackedFile() { var path = WriteText("delete.txt", "old"); AssetDatabase.Refresh(); File.Delete(FullPath(path)); AssetDatabase.Refresh(); Assert.Null(AssetDatabase.LoadAssetAtPath<TextAsset>(path)); }
    [Fact] public void Refresh_DuringAssetEditing_DefersNewAssetImport() { var path = WriteText("deferred.txt", "new"); AssetDatabase.StartAssetEditing(); AssetDatabase.Refresh(); Assert.Null(AssetDatabase.LoadAssetAtPath<TextAsset>(path)); AssetDatabase.StopAssetEditing(); Assert.Equal("new", AssetDatabase.LoadAssetAtPath<TextAsset>(path)!.text); }
    [Fact] public void Refresh_WithOptions_DiscoversAsset() { var path = WriteText("options.txt", "options"); AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); Assert.Equal("options", AssetDatabase.LoadAssetAtPath<TextAsset>(path)!.text); }
    [Fact] public void Refresh_NestedEditing_WaitsForOuterStop() { var path = WriteText("nested.txt", "nested"); AssetDatabase.StartAssetEditing(); AssetDatabase.StartAssetEditing(); AssetDatabase.Refresh(); AssetDatabase.StopAssetEditing(); Assert.Null(AssetDatabase.LoadAssetAtPath<TextAsset>(path)); AssetDatabase.StopAssetEditing(); Assert.NotNull(AssetDatabase.LoadAssetAtPath<TextAsset>(path)); }
    [Fact] public void Refresh_DoesNotImportFilesOutsideAssets() { WriteRaw("Library/ignored.txt", "ignored"); AssetDatabase.Refresh(); Assert.Null(AssetDatabase.LoadAssetAtPath<TextAsset>("Library/ignored.txt")); }

    private string WriteText(string name, string contents, string? metaLine = null)
    {
        var path = "Assets/Refresh/" + Guid.NewGuid().ToString("N") + "/" + name;
        WriteRaw(path, contents);
        if (metaLine is not null) WriteRaw(path + ".meta", "fileFormatVersion: 2\n" + metaLine + "\n");
        return path;
    }

    private string WritePng(string name)
    {
        var path = "Assets/Refresh/" + Guid.NewGuid().ToString("N") + "/" + name;
        var fullPath = FullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL8+QAAAABJRU5ErkJggg=="));
        return path;
    }

    private void WriteRaw(string path, string contents)
    {
        var fullPath = FullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, contents);
    }

    private string FullPath(string path) => Path.Combine(_dir, path);
}
