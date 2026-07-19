using System;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>In-memory main/sub-asset relationships for the public AssetDatabase contract.</summary>
[Collection(AssetPipelineStateCollection.Name)]
public sealed class AssetDatabaseSubAssetTests
{
    [Fact]
    public void AddObjectToAsset_PreservesTheMainAsset()
    {
        var (path, main) = CreateMain();
        AssetDatabase.AddObjectToAsset(new TextAsset("child"), path);

        Assert.Same(main, AssetDatabase.LoadMainAssetAtPath(path));
    }

    [Fact]
    public void LoadAllAssetsAtPath_ReturnsMainThenSubAssets()
    {
        var (path, main) = CreateMain();
        var first = new TextAsset("first");
        var second = new TextAsset("second");
        AssetDatabase.AddObjectToAsset(first, path);
        AssetDatabase.AddObjectToAsset(second, path);

        Assert.Equal(new UnityEngine.Object[] { main, first, second }, AssetDatabase.LoadAllAssetsAtPath(path));
    }

    [Fact]
    public void LoadAllAssetRepresentationsAtPath_ExcludesTheMainAsset()
    {
        var (path, _) = CreateMain();
        var child = new TextAsset("child");
        AssetDatabase.AddObjectToAsset(child, path);

        Assert.Equal(new UnityEngine.Object[] { child }, AssetDatabase.LoadAllAssetRepresentationsAtPath(path));
    }

    [Fact]
    public void GetAssetPath_ReturnsTheContainingAssetPathForSubAsset()
    {
        var (path, _) = CreateMain();
        var child = new TextAsset("child");
        AssetDatabase.AddObjectToAsset(child, path);

        Assert.Equal(path, AssetDatabase.GetAssetPath(child));
    }

    [Fact]
    public void Contains_ReturnsTrueForSubAsset()
    {
        var (path, _) = CreateMain();
        var child = new TextAsset("child");
        AssetDatabase.AddObjectToAsset(child, path);

        Assert.True(AssetDatabase.Contains(child));
    }

    [Fact]
    public void IsSubAsset_DistinguishesMainAndSubAsset()
    {
        var (path, main) = CreateMain();
        var child = new TextAsset("child");
        AssetDatabase.AddObjectToAsset(child, path);

        Assert.False(AssetDatabase.IsSubAsset(main));
        Assert.True(AssetDatabase.IsSubAsset(child));
    }

    [Fact]
    public void AddObjectToAsset_ObjectTarget_UsesTargetAssetPath()
    {
        var (path, main) = CreateMain();
        var child = new TextAsset("child");

        AssetDatabase.AddObjectToAsset(child, main);

        Assert.Equal(path, AssetDatabase.GetAssetPath(child));
    }

    [Fact]
    public void AddObjectToAsset_DoesNotDuplicateTheSameObject()
    {
        var (path, _) = CreateMain();
        var child = new TextAsset("child");
        AssetDatabase.AddObjectToAsset(child, path);
        AssetDatabase.AddObjectToAsset(child, path);

        Assert.Single(AssetDatabase.LoadAllAssetRepresentationsAtPath(path));
    }

    [Fact]
    public void AddObjectToAsset_RejectsUnknownPath()
    {
        Assert.Throws<ArgumentException>(() => AssetDatabase.AddObjectToAsset(new TextAsset("child"), UniquePath()));
    }

    [Fact]
    public void GetSubObjectsAtGUID_ReturnsOnlySubAssets()
    {
        var (path, _) = CreateMain();
        var child = new TextAsset("child");
        AssetDatabase.AddObjectToAsset(child, path);

        Assert.Equal(new object[] { child }, AssetDatabase.GetSubObjectsAtGUID(AssetDatabase.AssetPathToGUID(path)));
    }

    [Fact]
    public void MoveAsset_KeepsSubAssetRelationships()
    {
        var (path, _) = CreateMain();
        var child = new TextAsset("child");
        AssetDatabase.AddObjectToAsset(child, path);
        var target = UniquePath();

        Assert.Equal(target, AssetDatabase.MoveAsset(path, target));
        Assert.Equal(target, AssetDatabase.GetAssetPath(child));
        Assert.True(AssetDatabase.IsSubAsset(child));
    }

    [Fact]
    public void DeleteAsset_RemovesSubAssetRelationships()
    {
        var (path, _) = CreateMain();
        var child = new TextAsset("child");
        AssetDatabase.AddObjectToAsset(child, path);

        Assert.True(AssetDatabase.DeleteAsset(path));
        Assert.False(AssetDatabase.Contains(child));
        Assert.Empty(AssetDatabase.LoadAllAssetRepresentationsAtPath(path));
    }

    [Fact]
    public void CreateAsset_ReplacesPriorSubAssetSet()
    {
        var (path, _) = CreateMain();
        AssetDatabase.AddObjectToAsset(new TextAsset("old"), path);
        var replacement = new TextAsset("replacement");

        AssetDatabase.CreateAsset(replacement, path);

        Assert.Same(replacement, AssetDatabase.LoadMainAssetAtPath(path));
        Assert.Empty(AssetDatabase.LoadAllAssetRepresentationsAtPath(path));
    }

    private static (string Path, TextAsset Main) CreateMain()
    {
        var path = UniquePath();
        var main = new TextAsset("main");
        AssetDatabase.CreateAsset(main, path);
        return (path, main);
    }

    private static string UniquePath() => "Assets/SubAssets/" + Guid.NewGuid().ToString("N") + ".asset";
}
