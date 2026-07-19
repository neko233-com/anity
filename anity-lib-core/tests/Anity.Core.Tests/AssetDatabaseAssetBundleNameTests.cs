using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Xunit;
using EditorAssetImporter = UnityEditor.AssetImporter;

namespace Anity.Core.Tests;

/// <summary>AssetBundle name and variant registry coverage for AssetDatabase and AssetImporter.</summary>
[Collection(AssetPipelineStateCollection.Name)]
public sealed class AssetDatabaseAssetBundleNameTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anity-asset-bundle-names-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public AssetDatabaseAssetBundleNameTests()
    {
        Directory.CreateDirectory(_dir);
        EditorApplication.OpenProject(_dir);
    }

    public void Dispose()
    {
        EditorApplication.OpenProject(_originalDirectory);
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact] public void SetAssetBundleNameAndVariant_AssignsName() { var path = Create("name.asset"); AssetDatabase.SetAssetBundleNameAndVariant(path, "ui"); Assert.Equal("ui", AssetDatabase.GetAssetBundleName(path)); }
    [Fact] public void AssetImporter_AssignsAndReadsVariant() { var path = Create("variant.asset"); var importer = EditorAssetImporter.GetAtPath(path); importer.assetBundleName = "characters"; importer.assetBundleVariant = "hd"; Assert.Equal("characters", importer.assetBundleName); Assert.Equal("hd", importer.assetBundleVariant); }
    [Fact] public void GetAssetBundleName_ObjectOverloadFindsMainAsset() { var path = Create("object.asset"); AssetDatabase.SetAssetBundleNameAndVariant(path, "object-bundle"); Assert.Equal("object-bundle", AssetDatabase.GetAssetBundleName(AssetDatabase.LoadMainAssetAtPath(path)!)); }
    [Fact] public void GetAssetPathsFromAssetBundle_UsesFullNameIncludingVariant() { var path = Create("query.asset"); AssetDatabase.SetAssetBundleNameAndVariant(path, "textures", "mobile"); Assert.Equal(new[] { path }, AssetDatabase.GetAssetPathsFromAssetBundle("textures.mobile")); }
    [Fact] public void GetAllAssetBundleNames_IsSortedAndUnique() { var first = Create("sorted-first.asset"); var second = Create("sorted-second.asset"); AssetDatabase.SetAssetBundleNameAndVariant(second, "zebra"); AssetDatabase.SetAssetBundleNameAndVariant(first, "alpha"); Assert.Equal(new[] { "alpha", "zebra" }, AssetDatabase.GetAllAssetBundleNames()); }
    [Fact] public void ClearAssignment_MakesPreviousNameUnused() { var path = Create("unused.asset"); AssetDatabase.SetAssetBundleNameAndVariant(path, "unused"); AssetDatabase.SetAssetBundleNameAndVariant(path, ""); Assert.Equal(new[] { "unused" }, AssetDatabase.GetUnusedAssetBundleNames()); }
    [Fact] public void RemoveUnusedAssetBundleNames_RemovesOnlyUnusedNames() { var used = Create("used.asset"); var unused = Create("unused-remove.asset"); AssetDatabase.SetAssetBundleNameAndVariant(used, "used"); AssetDatabase.SetAssetBundleNameAndVariant(unused, "unused"); AssetDatabase.SetAssetBundleNameAndVariant(unused, ""); AssetDatabase.RemoveUnusedAssetBundleNames(); Assert.Equal(new[] { "used" }, AssetDatabase.GetAllAssetBundleNames()); }
    [Fact] public void MoveAsset_PreservesAssignment() { var source = Create("move.asset"); var target = "Assets/Bundles/Moved" + Guid.NewGuid().ToString("N") + ".asset"; AssetDatabase.SetAssetBundleNameAndVariant(source, "move", "hi"); Assert.Equal(target, AssetDatabase.MoveAsset(source, target)); Assert.Equal("move", AssetDatabase.GetAssetBundleName(target)); Assert.Equal("hi", EditorAssetImporter.GetAtPath(target).assetBundleVariant); }
    [Fact] public void DeleteAsset_MakesBundleNameUnused() { var path = Create("delete.asset"); AssetDatabase.SetAssetBundleNameAndVariant(path, "delete"); Assert.True(AssetDatabase.DeleteAsset(path)); Assert.Equal(new[] { "delete" }, AssetDatabase.GetUnusedAssetBundleNames()); }
    [Fact] public void AssetImporter_SaveSettings_PersistsNameAndVariantAcrossProjectSession() { var path = Track("persist.txt"); var importer = EditorAssetImporter.GetAtPath(path); importer.assetBundleName = "persistent"; importer.assetBundleVariant = "web"; importer.SaveSettings(); RestartProject(); AssetDatabase.ImportAsset(path); var restored = EditorAssetImporter.GetAtPath(path); Assert.Equal("persistent", restored.assetBundleName); Assert.Equal("web", restored.assetBundleVariant); }
    [Fact] public void GetAssetPathsFromAssetBundle_SceneFilterSeparatesSceneAssets() { var scene = Create("level.unity"); var data = Create("data.asset"); AssetDatabase.SetAssetBundleNameAndVariant(scene, "mixed"); AssetDatabase.SetAssetBundleNameAndVariant(data, "mixed"); Assert.Equal(new[] { scene }, AssetDatabase.GetAssetPathsFromAssetBundle("mixed", true)); Assert.Equal(new[] { data }, AssetDatabase.GetAssetPathsFromAssetBundle("mixed", false)); }

    private string Create(string name)
    {
        var path = "Assets/Bundles/" + Guid.NewGuid().ToString("N") + "/" + name;
        AssetDatabase.CreateAsset(new TextAsset(name), path);
        return path;
    }

    private string Track(string name)
    {
        var path = "Assets/Bundles/" + Guid.NewGuid().ToString("N") + "/" + name;
        var fullPath = Path.Combine(_dir, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "asset bundle persistence");
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private void RestartProject()
    {
        var transition = Path.Combine(_dir, "Library", "AssetBundleSessionTransition");
        Directory.CreateDirectory(transition);
        EditorApplication.OpenProject(transition);
        EditorApplication.OpenProject(_dir);
    }
}
