using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>AssetDatabase edit transactions — deferred disk reimport, nesting and queue behavior.</summary>
[Collection(AssetPipelineStateCollection.Name)]
public sealed class AssetEditingBatchTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-editing-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public AssetEditingBatchTests()
    {
        Directory.CreateDirectory(_dir);
        EditorApplication.OpenProject(_dir);
        PackageImportProbe.Reset();
    }

    public void Dispose()
    {
        PackageImportProbe.Reset();
        EditorApplication.OpenProject(_originalDirectory);
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact] public void ImportAsset_IsDeferredUntilStopAssetEditing()
    {
        var path = Track("deferred.txt", "old"); Write(path, "new");
        AssetDatabase.StartAssetEditing(); AssetDatabase.ImportAsset(path);
        Assert.Equal("old", Text(path));
        AssetDatabase.StopAssetEditing();
        Assert.Equal("new", Text(path));
    }

    [Fact] public void RepeatedImport_UsesLatestDiskContentsOnceFlushed()
    {
        var path = Track("repeat.txt", "old");
        AssetDatabase.StartAssetEditing();
        Write(path, "first"); AssetDatabase.ImportAsset(path);
        Write(path, "last"); AssetDatabase.ImportAsset(path);
        AssetDatabase.StopAssetEditing();
        Assert.Equal("last", Text(path));
    }

    [Fact] public void NestedEditing_WaitsForOuterStop()
    {
        var path = Track("nested.txt", "old"); Write(path, "new");
        AssetDatabase.StartAssetEditing(); AssetDatabase.StartAssetEditing(); AssetDatabase.ImportAsset(path);
        AssetDatabase.StopAssetEditing();
        Assert.Equal("old", Text(path));
        AssetDatabase.StopAssetEditing();
        Assert.Equal("new", Text(path));
    }

    [Fact] public void StopWithoutStart_IsHarmless()
    {
        var path = Track("idle.txt", "old"); Write(path, "new");
        AssetDatabase.StopAssetEditing();
        Assert.Equal("old", Text(path));
    }

    [Fact] public void ImportAsset_OutsideEditingRefreshesImmediately()
    {
        var path = Track("immediate.txt", "old"); Write(path, "new");
        AssetDatabase.ImportAsset(path);
        Assert.Equal("new", Text(path));
    }

    [Fact] public void ImportAssetWithOptions_IsAlsoDeferred()
    {
        var path = Track("options.txt", "old"); Write(path, "new");
        AssetDatabase.StartAssetEditing(); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        Assert.Equal("old", Text(path));
        AssetDatabase.StopAssetEditing();
        Assert.Equal("new", Text(path));
    }

    [Fact] public void Refresh_DuringEditingDefersAllTrackedAssets()
    {
        var first = Track("refresh-a.txt", "a-old"); var second = Track("refresh-b.txt", "b-old");
        Write(first, "a-new"); Write(second, "b-new");
        AssetDatabase.StartAssetEditing(); AssetDatabase.Refresh();
        Assert.Equal("a-old", Text(first)); Assert.Equal("b-old", Text(second));
        AssetDatabase.StopAssetEditing();
        Assert.Equal("a-new", Text(first)); Assert.Equal("b-new", Text(second));
    }

    [Fact] public void Refresh_OutsideEditingRefreshesTrackedAssets()
    {
        var path = Track("refresh-now.txt", "old"); Write(path, "new");
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Assert.Equal("new", Text(path));
    }

    [Fact] public void Queue_IsFlushedInDeterministicPathOrder()
    {
        var root = "Assets/EditingOrder/" + Guid.NewGuid().ToString("N") + "/";
        var z = TrackAt(root + "z-last.txt", "z-old"); var a = TrackAt(root + "a-first.txt", "a-old");
        Write(z, "z-new"); Write(a, "a-new"); PackageImportProbe.Enabled = true;
        AssetDatabase.StartAssetEditing(); AssetDatabase.ImportAsset(z); AssetDatabase.ImportAsset(a); AssetDatabase.StopAssetEditing();
        Assert.Equal(new[] { "pre:" + a, "post:" + a, "pre:" + z, "post:" + z }, PackageImportProbe.Calls);
    }

    [Fact] public void MissingDiskFile_DoesNotReplaceTrackedAsset()
    {
        var path = Track("missing.txt", "old"); File.Delete(FullPath(path));
        AssetDatabase.StartAssetEditing(); AssetDatabase.ImportAsset(path); AssetDatabase.StopAssetEditing();
        Assert.Equal("old", Text(path));
    }

    private string Track(string name, string contents)
    {
        return TrackAt("Assets/Editing/" + Guid.NewGuid().ToString("N") + "/" + name, contents);
    }

    private string TrackAt(string path, string contents)
    {
        Write(path, contents);
        AssetDatabase.CreateAsset(new TextAsset(contents), path);
        return path;
    }

    private void Write(string path, string contents)
    {
        var fullPath = FullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, contents);
    }

    private string FullPath(string path) => Path.Combine(_dir, path);
    private static string Text(string path) => AssetDatabase.LoadAssetAtPath<TextAsset>(path)!.text;
}
